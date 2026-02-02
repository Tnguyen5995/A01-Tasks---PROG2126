/*  
 *  FILE          : Program.cs
 *  PROJECT       : PROG2126 - Assignment - Task TCP/IP Performance
 *  PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli, Mohid Ali
 *  FIRST VERSION : 2026-01-28
 *  DESCRIPTION   :
 *    Entry point for the TCP logging server. The server accepts multiple clients,
 *    writes all client messages to one local file, and stops gracefully when the
 *    file reaches a configured maximum size.
 */

using System;
using Common;

namespace ServerApp
{
    internal static class ServerProgram
    {
        private static int Main(string[] args)
        {
            int exitCode = 0;

            int port = AppConstants.DefaultPort;
            int maxMb = 200;
            string logFilePath = "ServerLog.txt";
            ConcurrencyMode concurrencyMode = ConcurrencyMode.Task;

            try
            {
                ParseArgs(
                    args,
                    out port,
                    out maxMb,
                    out logFilePath,
                    out concurrencyMode);

                TcpLoggingServer server = new TcpLoggingServer(
                    port,
                    logFilePath,
                    maxMb,
                    concurrencyMode);

                server.Run();
            }
            catch (Exception exception)
            {
                Console.WriteLine("SERVER FATAL ERROR: " + exception.Message);
                exitCode = 1;
            }

            return (exitCode);
        }

        //
        // FUNCTION      : ParseArgs
        // DESCRIPTION   :
        //   Parses server command line arguments.
        // PARAMETERS    :
        //   string[] args                      : Input args
        //   int port                           : Output port
        //   int maxMb                          : Output max log size in MB
        //   string logFilePath                 : Output log file path
        //   ConcurrencyMode concurrencyMode    : Output concurrency mode
        // RETURNS       :
        //   void
        //
        private static void ParseArgs(
            string[] args,
            out int port,
            out int maxMb,
            out string logFilePath,
            out ConcurrencyMode concurrencyMode)
        {
            port = AppConstants.DefaultPort;
            maxMb = 5;
            logFilePath = "ServerLog.txt";
            concurrencyMode = ConcurrencyMode.Task;

            int index = 0;

            while (index < args.Length)
            {
                string key = args[index];

                if ((key == "--port") && ((index + 1) < args.Length))
                {
                    port = Convert.ToInt32(args[index + 1]);
                    index = index + 2;
                }
                else if ((key == "--maxMb") && ((index + 1) < args.Length))
                {
                    maxMb = Convert.ToInt32(args[index + 1]);
                    index = index + 2;
                }
                else if ((key == "--log") && ((index + 1) < args.Length))
                {
                    logFilePath = args[index + 1];
                    index = index + 2;
                }
                else if ((key == "--mode") && ((index + 1) < args.Length))
                {
                    string modeText = args[index + 1].Trim().ToLowerInvariant();

                    if (modeText == "thread")
                    {
                        concurrencyMode = ConcurrencyMode.Thread;
                    }
                    else
                    {
                        concurrencyMode = ConcurrencyMode.Task;
                    }

                    index = index + 2;
                }
                else
                {
                    index = index + 1;
                }
            }

            return;
        }
    }
}
