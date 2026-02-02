/*  
 *  FILE          : AppConstants.cs
 *  PROJECT       : PROG2126 - Assignment - Task TCP/IP Performance
 *  PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli, Mohid Ali
 *  FIRST VERSION : 2026-01-20
 *  DESCRIPTION   :
 *    This file defines constants shared by the server and client applications,
 *    including protocol tokens and safe size limits.
 */

namespace Common
{
    public static class AppConstants
    {
        public const int DefaultPort = 5000;

        public const int MaxMessageBytes = 1024 * 1024;   // 1 MB safety limit
        public const int SocketReadTimeoutMs = 1000;
        public const int SocketWriteTimeoutMs = 1000;

        public const string ResponseOk = "OK";
        public const string ResponseStop = "STOP";
    }
}
