/*  
 *  FILE          : TcpLoggingServer.cs
 *  PROJECT       : PROG2126 - Assignment - Task TCP/IP Performance
 *  PROGRAMMER    : Tuan Thanh Nguyen
 *  FIRST VERSION : 2026-01-28
 *  DESCRIPTION   :
 *    Implements a TCP server that:
 *      - Accepts multiple clients (from other computers)
 *      - Receives synchronous messages and replies synchronously (OK/STOP)
 *      - Writes all client messages to a single local file
 *      - Stops gracefully when the file reaches a configured size
 *      - Supports Task vs Thread modes for comparison
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace ServerApp
{
    internal sealed class TcpLoggingServer
    {
        private readonly int _port;
        private readonly string _logFilePath;
        private readonly long _maxFileBytes;
        private readonly ConcurrencyMode _concurrencyMode;

        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;

        private readonly object _fileLock;
        private FileStream _fileStream;
        private StreamWriter _streamWriter;

        private readonly object _clientListLock;
        private readonly List<Task> _clientTasks;
        private readonly List<Thread> _clientThreads;

        private long _currentFileBytes;
        private int _shutdownRequestedFlag;

        private long _messageCount;
        private readonly Stopwatch _serverStopwatch;
        private readonly TimeMetrics _serverWriteTiming;

        /*
         *   NAME    : TcpLoggingServer
         *   PURPOSE : The TcpLoggingServer class models a TCP server that receives messages from
         *             multiple clients concurrently (Task or Thread) while keeping each client
         *             connection synchronous (request/response). All client communications are
         *             appended to a single log file. Once the log file reaches a configured size,
         *             the server responds STOP to clients and shuts down gracefully.
         */
        public TcpLoggingServer(
            int port,
            string logFilePath,
            int maxMb,
            ConcurrencyMode concurrencyMode)
        {
            _port = port;
            _logFilePath = logFilePath;
            _maxFileBytes = (long)maxMb * 1024L * 1024L;
            _concurrencyMode = concurrencyMode;

            _listener = null;
            _cancellationTokenSource = null;

            _fileLock = new object();
            _fileStream = null;
            _streamWriter = null;

            _clientListLock = new object();
            _clientTasks = new List<Task>();
            _clientThreads = new List<Thread>();

            _currentFileBytes = 0;
            _shutdownRequestedFlag = 0;

            _messageCount = 0;
            _serverStopwatch = new Stopwatch();
            _serverWriteTiming = new TimeMetrics();
        }

        //
        // FUNCTION      : Run
        // DESCRIPTION   :
        //   Starts the server and blocks until the server stops (file full or fatal error).
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        public void Run()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _serverStopwatch.Restart();

            OpenLogFile();

            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            Console.WriteLine("Server started on port " + _port);
            Console.WriteLine("Log file: " + _logFilePath);
            Console.WriteLine("Max bytes: " + _maxFileBytes);
            Console.WriteLine("Mode: " + _concurrencyMode);

            bool isServerDone = false;

            while (isServerDone == false)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    ConfigureSocket(client);

                    StartClientHandler(client);

                    if (IsShutdownRequested() == true)
                    {
                        isServerDone = true;
                    }
                }
                catch (SocketException)
                {
                    if (IsShutdownRequested() == true)
                    {
                        isServerDone = true;
                    }
                    else
                    {
                        isServerDone = true;
                        RequestShutdown();
                    }
                }
                catch (Exception)
                {
                    isServerDone = true;
                    RequestShutdown();
                }
            }

            WaitForClientWorkersToFinish();
            CloseLogFile();

            _serverStopwatch.Stop();

            PrintSummary();

            return;
        }

        //
        // FUNCTION      : StartClientHandler
        // DESCRIPTION   :
        //   Starts a client handler using Task or Thread mode.
        // PARAMETERS    :
        //   TcpClient client : The accepted TCP client
        // RETURNS       :
        //   void
        //
        private void StartClientHandler(TcpClient client)
        {
            if (_concurrencyMode == ConcurrencyMode.Thread)
            {
                Thread clientThread = new Thread(
                    () => HandleClient(client, _cancellationTokenSource.Token));

                clientThread.IsBackground = true;

                lock (_clientListLock)
                {
                    _clientThreads.Add(clientThread);
                }

                clientThread.Start();
            }
            else
            {
                Task clientTask = Task.Run(
                    () => HandleClient(client, _cancellationTokenSource.Token));

                lock (_clientListLock)
                {
                    _clientTasks.Add(clientTask);
                }
            }

            return;
        }

        //
        // FUNCTION      : HandleClient
        // DESCRIPTION   :
        //   Handles one client connection synchronously:
        //     - Receive one message
        //     - Append to log
        //     - Reply OK or STOP
        //   Stops gracefully when shutdown requested.
        // PARAMETERS    :
        //   TcpClient client              : Connected client
        //   CancellationToken cancellationToken : Cancellation token
        // RETURNS       :
        //   void
        //
        private void HandleClient(
            TcpClient client,
            CancellationToken cancellationToken)
        {
            string remoteEndpoint = "unknown";

            try
            {
                if (client.Client.RemoteEndPoint != null)
                {
                    remoteEndpoint = client.Client.RemoteEndPoint.ToString();
                }

                using (client)
                {
                    NetworkStream networkStream = client.GetStream();
                    networkStream.ReadTimeout = AppConstants.SocketReadTimeoutMs;
                    networkStream.WriteTimeout = AppConstants.SocketWriteTimeoutMs;

                    bool isClientDone = false;

                    while (isClientDone == false)
                    {
                        if ((cancellationToken.IsCancellationRequested == true) ||
                            (IsShutdownRequested() == true))
                        {
                            isClientDone = true;
                        }
                        else
                        {
                            string requestMessage;
                            string receiveError;

                            bool received = MessageProtocol.ReceiveStringMessage(
                                networkStream,
                                AppConstants.MaxMessageBytes,
                                out requestMessage,
                                out receiveError);

                            if (received == true)
                            {
                                bool writeOk = AppendLogLine(remoteEndpoint, requestMessage);

                                if (writeOk == false)
                                {
                                    RequestShutdown();
                                }

                                Interlocked.Increment(ref _messageCount);

                                if (_currentFileBytes >= _maxFileBytes)
                                {
                                    RequestShutdown();
                                }

                                string responseText = AppConstants.ResponseOk;

                                if (IsShutdownRequested() == true)
                                {
                                    responseText = AppConstants.ResponseStop;
                                }

                                string sendError;

                                bool sent = MessageProtocol.SendStringMessage(
                                    networkStream,
                                    responseText,
                                    AppConstants.MaxMessageBytes,
                                    out sendError);

                                if (sent == false)
                                {
                                    isClientDone = true;
                                }

                                if (responseText == AppConstants.ResponseStop)
                                {
                                    isClientDone = true;
                                }
                            }
                            else
                            {
                                if (receiveError.ToLowerInvariant().Contains("timeout") == true)
                                {
                                    // Non-fatal: let loop continue and re-check cancellation/shutdown.
                                }
                                else
                                {
                                    isClientDone = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Keep shutdown graceful; do not crash the server due to one client.
            }

            return;
        }

        //
        // FUNCTION      : AppendLogLine
        // DESCRIPTION   :
        //   Appends a single line to the shared log file in a thread-safe manner,
        //   updating the current file size.
        // PARAMETERS    :
        //   string remoteEndpoint : Client endpoint
        //   string message        : Received message
        // RETURNS       :
        //   bool : True if the write succeeded, false otherwise
        //
        private bool AppendLogLine(string remoteEndpoint, string message)
        {
            bool isSuccessful = false;

            try
            {
                long startTicks = Stopwatch.GetTimestamp();

                lock (_fileLock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string line = timestamp + " | " + remoteEndpoint + " | " + message;

                    _streamWriter.WriteLine(line);
                    _streamWriter.Flush();

                    _currentFileBytes = _fileStream.Length;
                }

                long endTicks = Stopwatch.GetTimestamp();
                long elapsedTicks = endTicks - startTicks;
                _serverWriteTiming.AddSample(elapsedTicks);

                isSuccessful = true;
            }
            catch (Exception)
            {
                isSuccessful = false;
            }

            return (isSuccessful);
        }

        //
        // FUNCTION      : OpenLogFile
        // DESCRIPTION   :
        //   Opens the shared log file for append.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        private void OpenLogFile()
        {
            lock (_fileLock)
            {
                _fileStream = new FileStream(
                    _logFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read);

                _streamWriter = new StreamWriter(_fileStream);
                _streamWriter.AutoFlush = true;

                _currentFileBytes = _fileStream.Length;
            }

            return;
        }

        //
        // FUNCTION      : CloseLogFile
        // DESCRIPTION   :
        //   Closes the log file resources.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        private void CloseLogFile()
        {
            lock (_fileLock)
            {
                if (_streamWriter != null)
                {
                    _streamWriter.Flush();
                    _streamWriter.Dispose();
                    _streamWriter = null;
                }

                if (_fileStream != null)
                {
                    _fileStream.Dispose();
                    _fileStream = null;
                }
            }

            return;
        }

        //
        // FUNCTION      : ConfigureSocket
        // DESCRIPTION   :
        //   Configures basic socket options for a new client.
        // PARAMETERS    :
        //   TcpClient client : The client to configure
        // RETURNS       :
        //   void
        //
        private void ConfigureSocket(TcpClient client)
        {
            client.NoDelay = true;
            client.ReceiveBufferSize = 64 * 1024;
            client.SendBufferSize = 64 * 1024;

            return;
        }

        //
        // FUNCTION      : RequestShutdown
        // DESCRIPTION   :
        //   Requests a shutdown once (thread-safe) and stops the listener so Accept will exit.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        private void RequestShutdown()
        {
            int previous = Interlocked.CompareExchange(ref _shutdownRequestedFlag, 1, 0);

            if (previous == 0)
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                }

                if (_listener != null)
                {
                    try
                    {
                        _listener.Stop();
                    }
                    catch (Exception)
                    {
                        // Ignore listener stop failures during shutdown.
                    }
                }
            }

            return;
        }

        //
        // FUNCTION      : IsShutdownRequested
        // DESCRIPTION   :
        //   Returns whether shutdown has been requested.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   bool : True if shutdown requested, false otherwise
        //
        private bool IsShutdownRequested()
        {
            bool isRequested = false;

            if (_shutdownRequestedFlag != 0)
            {
                isRequested = true;
            }

            return (isRequested);
        }

        //
        // FUNCTION      : WaitForClientWorkersToFinish
        // DESCRIPTION   :
        //   Waits for all client workers (tasks/threads) to finish for graceful shutdown.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        private void WaitForClientWorkersToFinish()
        {
            if (_concurrencyMode == ConcurrencyMode.Thread)
            {
                lock (_clientListLock)
                {
                    foreach (Thread clientThread in _clientThreads)
                    {
                        try
                        {
                            clientThread.Join(2000);
                        }
                        catch (Exception)
                        {
                            // Ignore join failures
                        }
                    }
                }
            }
            else
            {
                Task[] tasksToWait = new Task[0];

                lock (_clientListLock)
                {
                    tasksToWait = _clientTasks.ToArray();
                }

                try
                {
                    Task.WaitAll(tasksToWait, 2000);
                }
                catch (Exception)
                {
                    // Ignore wait failures
                }
            }

            return;
        }

        //
        // FUNCTION      : PrintSummary
        // DESCRIPTION   :
        //   Prints performance metrics (including time measurement) at shutdown.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        private void PrintSummary()
        {
            double seconds = _serverStopwatch.Elapsed.TotalSeconds;
            double messagesPerSecond = 0.0;
            double bytesPerSecond = 0.0;

            if (seconds > 0.0)
            {
                messagesPerSecond = (double)_messageCount / seconds;
                bytesPerSecond = (double)_currentFileBytes / seconds;
            }

            Console.WriteLine();
            Console.WriteLine("===== SERVER SUMMARY =====");
            Console.WriteLine("Total messages: " + _messageCount);
            Console.WriteLine("Final log bytes: " + _currentFileBytes);
            Console.WriteLine("Elapsed seconds: " + seconds.ToString("F3"));
            Console.WriteLine("Msgs/sec: " + messagesPerSecond.ToString("F3"));
            Console.WriteLine("Bytes/sec: " + bytesPerSecond.ToString("F3"));
            Console.WriteLine("File-write timing: " + _serverWriteTiming.GetSummary(Stopwatch.Frequency));
            Console.WriteLine("==========================");

            return;
        }
    }
}
