/*  
 *  FILE          : ClientWorker.cs
 *  PROJECT       : PROG2126 - Assignment - Task TCP/IP Performance
 *  PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli, Mohid Ali
 *  FIRST VERSION : 2026-01-28
 *  DESCRIPTION   :
 *    Implements one client worker. Uses synchronous request/response TCP messages:
 *      send payload -> receive OK/STOP.
 *    Stops gracefully when STOP is received or cancellation is requested.
 */

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Common;

namespace ClientApp
{
    internal sealed class ClientWorker
    {
        private readonly string _serverIp;
        private readonly int _port;
        private readonly int _workerIndex;
        private readonly int _payloadBytes;
        private readonly int _delayMs;

        /*
         *   NAME    : ClientWorker
         *   PURPOSE : The ClientWorker class models a single synchronous TCP client that
         *             repeatedly sends data to the server and waits for a response. The
         *             worker stops when STOP is received or cancellation occurs.
         */
        public ClientWorker(
            string serverIp,
            int port,
            int workerIndex,
            int payloadBytes,
            int delayMs)
        {
            _serverIp = serverIp;
            _port = port;
            _workerIndex = workerIndex;
            _payloadBytes = payloadBytes;
            _delayMs = delayMs;
        }

        //
        // FUNCTION      : RunUntilStop
        // DESCRIPTION   :
        //   Connects to the server and sends messages until STOP is received.
        //   Records round-trip time (Stopwatch ticks) for each request/response.
        // PARAMETERS    :
        //   CancellationToken cancellationToken : Token to stop the worker
        //   TimeMetrics roundTripTiming         : Shared timing collector
        //   ref long totalMessagesSent          : Shared message counter
        // RETURNS       :
        //   bool : True if STOP received (suggest stop all workers), false otherwise
        //
        public bool RunUntilStop(
            CancellationToken cancellationToken,
            TimeMetrics roundTripTiming,
            ref long totalMessagesSent)
        {
            bool stopAllWorkers = false;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.NoDelay = true;
                    client.ReceiveBufferSize = 64 * 1024;
                    client.SendBufferSize = 64 * 1024;

                    client.Connect(_serverIp, _port);

                    NetworkStream networkStream = client.GetStream();
                    networkStream.ReadTimeout = AppConstants.SocketReadTimeoutMs;
                    networkStream.WriteTimeout = AppConstants.SocketWriteTimeoutMs;

                    bool isDone = false;
                    long sequence = 0;

                    while (isDone == false)
                    {
                        if (cancellationToken.IsCancellationRequested == true)
                        {
                            isDone = true;
                        }
                        else
                        {
                            string payload = BuildPayload(sequence);

                            long startTicks = Stopwatch.GetTimestamp();

                            string sendError;
                            bool sent = MessageProtocol.SendStringMessage(
                                networkStream,
                                payload,
                                AppConstants.MaxMessageBytes,
                                out sendError);

                            if (sent == true)
                            {
                                string response;
                                string receiveError;

                                bool received = MessageProtocol.ReceiveStringMessage(
                                    networkStream,
                                    AppConstants.MaxMessageBytes,
                                    out response,
                                    out receiveError);

                                long endTicks = Stopwatch.GetTimestamp();
                                long elapsedTicks = endTicks - startTicks;

                                roundTripTiming.AddSample(elapsedTicks);

                                if (received == true)
                                {
                                    Interlocked.Increment(ref totalMessagesSent);

                                    if (response == AppConstants.ResponseStop)
                                    {
                                        stopAllWorkers = true;
                                        isDone = true;
                                    }
                                }
                                else
                                {
                                    isDone = true;
                                }
                            }
                            else
                            {
                                isDone = true;
                            }

                            sequence = sequence + 1;

                            if ((_delayMs > 0) && (isDone == false))
                            {
                                Thread.Sleep(_delayMs);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Graceful stop on any connection error.
            }

            return (stopAllWorkers);
        }

        //
        // FUNCTION      : BuildPayload
        // DESCRIPTION   :
        //   Builds a payload string close to the configured byte size.
        // PARAMETERS    :
        //   long sequence : Message sequence number
        // RETURNS       :
        //   string : Payload string
        //
        private string BuildPayload(long sequence)
        {
            string header =
                "worker=" + _workerIndex +
                ", seq=" + sequence +
                ", data=";

            int headerBytes = Encoding.UTF8.GetByteCount(header);
            int remainingBytes = _payloadBytes - headerBytes;

            if (remainingBytes < 0)
            {
                remainingBytes = 0;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(header);

            int i = 0;

            while (i < remainingBytes)
            {
                builder.Append('X');
                i = i + 1;
            }

            return (builder.ToString());
        }
    }
}
