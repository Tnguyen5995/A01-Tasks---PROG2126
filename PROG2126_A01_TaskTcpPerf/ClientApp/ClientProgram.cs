/*  
 *  FILE          : Program.cs
 *  PROJECT       : PROG2126 - Assignment - Task TCP/IP Performance
 *  PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli, Mohid Ali
 *  FIRST VERSION : 2026-01-28
 *  DESCRIPTION   :
 *    Entry point for the TCP client load generator. A single client computer can
 *    run multiple client workers (Task or Thread). Each worker uses synchronous
 *    request/response TCP messaging to the server until STOP is received.
 */

using System;
using Common;

namespace ClientApp
{
    internal static class ClientProgram
    {
        private static int Main(string[] args)
        {
            int exitCode = 0;

            string serverIp = "127.0.0.1";
            int port = AppConstants.DefaultPort;
            int workers = 5;
            int payloadBytes = 256;
            int delayMs = 0;
            ConcurrencyMode concurrencyMode = ConcurrencyMode.Task;

            try
            {
                ParseArgs(
                    args,
                    out serverIp,
                    out port,
                    out workers,
                    out payloadBytes,
                    out delayMs,
                    out concurrencyMode);

                ClientLoadGenerator generator = new ClientLoadGenerator(
                    serverIp,
                    port,
                    workers,
                    payloadBytes,
                    delayMs,
                    concurrencyMode);

                generator.Run();
            }
            catch (Exception exception)
            {
                Console.WriteLine("CLIENT FATAL ERROR: " + exception.Message);
                exitCode = 1;
            }

            return (exitCode);
        }

        //
        // FUNCTION      : ParseArgs
        // DESCRIPTION   :
        //   Parses client command line arguments.
        // PARAMETERS    :
        //   string[] args                      : Input args
        //   string serverIp                    : Output server IP
        //   int port                           : Output server port
        //   int workers                        : Output number of client workers
        //   int payloadBytes                   : Output payload byte size
        //   int delayMs                        : Output delay between messages
        //   ConcurrencyMode concurrencyMode    : Output concurrency mode
        // RETURNS       :
        //   void
        //
        private static void ParseArgs(
            string[] args,
            out string serverIp,
            out int port,
            out int workers,
            out int payloadBytes,
            out int delayMs,
            out ConcurrencyMode concurrencyMode)
        {
            serverIp = "127.0.0.1"; //default server IP
            port = AppConstants.DefaultPort;
            workers = 5; //number of client connections to create
            payloadBytes = 256; //size of each message sent to the server
            delayMs = 0; //delay between messages
            concurrencyMode = ConcurrencyMode.Task; 

            int index = 0;
            //loop to go thorouhg all command line arguments one by one 
            while (index < args.Length)
            {
                //reads the current argument
                string key = args[index];

                //if the user specificed a server ip address
                if ((key == "--server") && ((index + 1) < args.Length))
                {
                    serverIp = args[index + 1];
                    index = index + 2;
                }
                //if user specified port
                else if ((key == "--port") && ((index + 1) < args.Length))
                {
                    port = Convert.ToInt32(args[index + 1]); //converts port value from text to integer
                    index = index + 2;
                }
                else if ((key == "--workers") && ((index + 1) < args.Length))
                {
                    workers = Convert.ToInt32(args[index + 1]); //number of simulated clients created
                    index = index + 2;
                }
                else if ((key == "--payloadBytes") && ((index + 1) < args.Length))
                {
                    payloadBytes = Convert.ToInt32(args[index + 1]); 
                    index = index + 2;
                }
                else if ((key == "--delayMs") && ((index + 1) < args.Length))
                {
                    delayMs = Convert.ToInt32(args[index + 1]);
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
