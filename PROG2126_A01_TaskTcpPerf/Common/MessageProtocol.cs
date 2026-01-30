/*  
 *  FILE          : MessageProtocol.cs
 *  PROJECT       : PROG2126 - Assignment - Task TCP/IP Performance
 *  PROGRAMMER    : Tuan Thanh Nguyen
 *  FIRST VERSION : 2026-01-28
 *  DESCRIPTION   :
 *    This file implements a simple length-prefixed TCP protocol:
 *    [4-byte length (big-endian)] [UTF-8 payload bytes].
 *    The protocol is synchronous: client sends 1 message, server replies 1 message.
 */

using System;
using System.Net.Sockets;
using System.Text;

namespace Common
{
    public static class MessageProtocol
    {
        // 
        // FUNCTION      : SendStringMessage
        // DESCRIPTION   :
        //   Sends a UTF-8 string as a length-prefixed message over a NetworkStream.
        // PARAMETERS    :
        //   NetworkStream networkStream : The stream to write to
        //   string message              : The message to send
        //   int maxMessageBytes         : Maximum allowed payload bytes
        //   string errorMessage         : Output error message on failure
        // RETURNS       :
        //   bool : True if send succeeded, false otherwise
        //
        public static bool SendStringMessage(
            NetworkStream networkStream,
            string message,
            int maxMessageBytes,
            out string errorMessage)
        {
            bool isSuccessful = false;
            errorMessage = string.Empty;

            try
            {
                byte[] payloadBytes = Encoding.UTF8.GetBytes(message);
                int payloadLength = payloadBytes.Length;

                if (payloadLength <= maxMessageBytes)
                {
                    byte[] lengthBytes = new byte[4];
                    lengthBytes[0] = (byte)((payloadLength >> 24) & 0xFF);
                    lengthBytes[1] = (byte)((payloadLength >> 16) & 0xFF);
                    lengthBytes[2] = (byte)((payloadLength >> 8) & 0xFF);
                    lengthBytes[3] = (byte)(payloadLength & 0xFF);

                    networkStream.Write(lengthBytes, 0, lengthBytes.Length);
                    networkStream.Write(payloadBytes, 0, payloadBytes.Length);
                    networkStream.Flush();

                    isSuccessful = true;
                }
                else
                {
                    errorMessage = "Message payload exceeds the maximum allowed bytes.";
                }
            }
            catch (Exception exception)
            {
                errorMessage = "Send failed: " + exception.Message;
            }

            return (isSuccessful);
        }

        //
        // FUNCTION      : ReceiveStringMessage
        // DESCRIPTION   :
        //   Receives a length-prefixed UTF-8 string message from a NetworkStream.
        // PARAMETERS    :
        //   NetworkStream networkStream : The stream to read from
        //   int maxMessageBytes         : Maximum allowed payload bytes
        //   string message              : Output received message
        //   string errorMessage         : Output error message on failure
        // RETURNS       :
        //   bool : True if receive succeeded, false otherwise
        //
        public static bool ReceiveStringMessage(
            NetworkStream networkStream,
            int maxMessageBytes,
            out string message,
            out string errorMessage)
        {
            bool isSuccessful = false;
            message = string.Empty;
            errorMessage = string.Empty;

            try
            {
                byte[] lengthBytes = new byte[4];
                int headerBytesRead = 0;

                bool headerReadOk = ReadExactly(
                    networkStream,
                    lengthBytes,
                    0,
                    4,
                    out headerBytesRead,
                    out errorMessage);

                if (headerReadOk == true)
                {
                    int payloadLength =
                        (lengthBytes[0] << 24) |
                        (lengthBytes[1] << 16) |
                        (lengthBytes[2] << 8) |
                        (lengthBytes[3]);

                    if ((payloadLength >= 0) && (payloadLength <= maxMessageBytes))
                    {
                        byte[] payloadBytes = new byte[payloadLength];
                        int payloadBytesRead = 0;

                        bool payloadReadOk = ReadExactly(
                            networkStream,
                            payloadBytes,
                            0,
                            payloadLength,
                            out payloadBytesRead,
                            out errorMessage);

                        if (payloadReadOk == true)
                        {
                            message = Encoding.UTF8.GetString(payloadBytes);
                            isSuccessful = true;
                        }
                    }
                    else
                    {
                        errorMessage = "Invalid payload length received.";
                    }
                }
            }
            catch (Exception exception)
            {
                errorMessage = "Receive failed: " + exception.Message;
            }

            return (isSuccessful);
        }

        //
        // FUNCTION      : ReadExactly
        // DESCRIPTION   :
        //   Attempts to read exactly 'count' bytes from the NetworkStream into buffer.
        //   This is required because TCP is a stream (not message) protocol.
        // PARAMETERS    :
        //   NetworkStream networkStream : The stream to read from
        //   byte[] buffer               : Destination buffer
        //   int offset                  : Offset into the destination buffer
        //   int count                   : Number of bytes to read
        //   int bytesReadTotal          : Total bytes read
        //   string errorMessage         : Output error message on failure
        // RETURNS       :
        //   bool : True if exactly count bytes were read, false otherwise
        //
        private static bool ReadExactly(
            NetworkStream networkStream,
            byte[] buffer,
            int offset,
            int count,
            out int bytesReadTotal,
            out string errorMessage)
        {
            bool isSuccessful = false;
            bytesReadTotal = 0;
            errorMessage = string.Empty;

            try
            {
                int totalRead = 0;
                bool isDone = false;

                while (isDone == false)
                {
                    int bytesRemaining = count - totalRead;

                    if (bytesRemaining > 0)
                    {
                        int bytesRead = networkStream.Read(
                            buffer,
                            offset + totalRead,
                            bytesRemaining);

                        if (bytesRead > 0)
                        {
                            totalRead = totalRead + bytesRead;

                            if (totalRead == count)
                            {
                                isDone = true;
                                isSuccessful = true;
                            }
                        }
                        else
                        {
                            errorMessage = "Remote endpoint closed the connection.";
                            isDone = true;
                        }
                    }
                    else
                    {
                        isDone = true;
                        isSuccessful = true;
                    }
                }

                bytesReadTotal = totalRead;
            }
            catch (Exception exception)
            {
                errorMessage = "ReadExactly failed: " + exception.Message;
            }

            return (isSuccessful);
        }
    }
}
