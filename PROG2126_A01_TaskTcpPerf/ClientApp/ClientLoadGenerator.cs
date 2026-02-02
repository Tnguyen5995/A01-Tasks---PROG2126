/*  
 *  FILE          : ClientLoadGenerator.cs
 *  PROJECT       : PROG2126 - Assignment - Task TCP/IP Performance
 *  PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli, Mohid Ali
 *  FIRST VERSION : 2026-01-20
 *  DESCRIPTION   :
 *    Runs multiple client workers (Task or Thread). Each worker connects to the server
 *    and repeatedly sends messages synchronously until STOP is received.
 *    This supports performance experiments with time-based metrics.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace ClientApp
{
    internal sealed class ClientLoadGenerator
    {
        private readonly string _serverIp;
        private readonly int _port;
        private readonly int _workers;
        private readonly int _payloadBytes;
        private readonly int _delayMs;
        private readonly ConcurrencyMode _concurrencyMode;

        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly List<Task> _workerTasks;
        private readonly List<Thread> _workerThreads;

        private long _totalMessagesSent;
        private readonly Stopwatch _stopwatch;
        private readonly TimeMetrics _roundTripTiming;

        /*
         *   NAME    : ClientLoadGenerator
         *   PURPOSE : The ClientLoadGenerator class models a client-side load runner that can
         *             launch multiple concurrent workers using either Tasks or Threads. Each
         *             worker uses synchronous TCP request/response to send data to the server
         *             and stops when the server replies STOP.
         */
        public ClientLoadGenerator(
            string serverIp,
            int port,
            int workers,
            int payloadBytes,
            int delayMs,
            ConcurrencyMode concurrencyMode)
        {
            _serverIp = serverIp;
            _port = port;
            _workers = workers;
            _payloadBytes = payloadBytes;
            _delayMs = delayMs;
            _concurrencyMode = concurrencyMode;

            _cancellationTokenSource = new CancellationTokenSource();

            _workerTasks = new List<Task>();
            _workerThreads = new List<Thread>();

            _totalMessagesSent = 0;
            _stopwatch = new Stopwatch();
            _roundTripTiming = new TimeMetrics();
        }

        //
        // FUNCTION      : Run
        // DESCRIPTION   :
        //   Starts the workers and blocks until they stop (STOP response or cancellation).
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        public void Run()
        {
            Console.WriteLine("Client targeting " + _serverIp + ":" + _port);
            Console.WriteLine("Workers: " + _workers);
            Console.WriteLine("PayloadBytes: " + _payloadBytes);
            Console.WriteLine("DelayMs: " + _delayMs);
            Console.WriteLine("Mode: " + _concurrencyMode);

            _stopwatch.Restart();

            StartWorkers();
            WaitForWorkers();

            _stopwatch.Stop();

            PrintSummary();

            return;
        }

        //
        // FUNCTION      : StartWorkers
        // DESCRIPTION   :
        //   Starts worker tasks or threads.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        private void StartWorkers()
        {
            int workerIndex = 0;

            while (workerIndex < _workers)
            {
                int capturedIndex = workerIndex;

                if (_concurrencyMode == ConcurrencyMode.Thread)
                {
                    Thread workerThread = new Thread(
                        () => RunWorker(capturedIndex, _cancellationTokenSource.Token));

                    workerThread.IsBackground = true;
                    _workerThreads.Add(workerThread);
                    workerThread.Start();
                }
                else
                {
                    Task workerTask = Task.Run(
                        () => RunWorker(capturedIndex, _cancellationTokenSource.Token));

                    _workerTasks.Add(workerTask);
                }

                workerIndex = workerIndex + 1;
            }

            return;
        }

        //
        // FUNCTION      : WaitForWorkers
        // DESCRIPTION   :
        //   Waits for all workers to complete.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        private void WaitForWorkers()
        {
            if (_concurrencyMode == ConcurrencyMode.Thread)
            {
                foreach (Thread workerThread in _workerThreads)
                {
                    try
                    {
                        workerThread.Join();
                    }
                    catch (Exception)
                    {
                        // Ignore join errors
                    }
                }
            }
            else
            {
                try
                {
                    Task.WaitAll(_workerTasks.ToArray());
                }
                catch (Exception)
                {
                    // Ignore wait errors
                }
            }

            return;
        }

        //
        // FUNCTION      : RunWorker
        // DESCRIPTION   :
        //   Worker loop: connect, send payload, receive OK/STOP, record round-trip time.
        // PARAMETERS    :
        //   int workerIndex                : Worker ID
        //   CancellationToken cancellationToken : Token used to stop worker
        // RETURNS       :
        //   void
        //
        private void RunWorker(int workerIndex, CancellationToken cancellationToken)
        {
            ClientWorker worker = new ClientWorker(
                _serverIp,
                _port,
                workerIndex,
                _payloadBytes,
                _delayMs);

            bool shouldStopAll = worker.RunUntilStop(
                cancellationToken,
                _roundTripTiming,
                ref _totalMessagesSent);

            if (shouldStopAll == true)
            {
                _cancellationTokenSource.Cancel();
            }

            return;
        }

        //
        // FUNCTION      : PrintSummary
        // DESCRIPTION   :
        //   Prints client-side performance summary including time-based metric.
        // PARAMETERS    :
        //   none
        // RETURNS       :
        //   void
        //
        private void PrintSummary()
        {
            double seconds = _stopwatch.Elapsed.TotalSeconds;
            double messagesPerSecond = 0.0;

            if (seconds > 0.0)
            {
                messagesPerSecond = (double)_totalMessagesSent / seconds;
            }

            Console.WriteLine();
            Console.WriteLine("===== CLIENT SUMMARY =====");
            Console.WriteLine("Total messages sent: " + _totalMessagesSent);
            Console.WriteLine("Elapsed seconds: " + seconds.ToString("F3"));
            Console.WriteLine("Msgs/sec: " + messagesPerSecond.ToString("F3"));
            Console.WriteLine("Round-trip timing: " + _roundTripTiming.GetSummary(Stopwatch.Frequency));
            Console.WriteLine("==========================");

            return;
        }
    }
}
