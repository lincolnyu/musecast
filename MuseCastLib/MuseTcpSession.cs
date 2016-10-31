using System;
using System.Net.Sockets;
using System.Text;

namespace MuseCastLib
{
    public class MuseTcpSession : ISession, IDisposable
    {
        public MuseTcpSession(Socket socket, string mimeType)
        {
            Socket = socket;
            MimeType = mimeType;
        }

        public Socket Socket { get; private set; }

        public string MimeType { get; }

        public bool Started => Socket.Connected;

        public string ClientHttpVersion { get; private set; }

        public void Close()
        {
            if (Socket != null)
            {
                Socket.Close();
                Socket = null;
            }
        }

        public void Dispose() => Close();

        public bool WaitForInitRequest()
        {
            var bufRecv = new byte[1024];
            Socket.Receive(bufRecv, bufRecv.Length, 0);

            // converts byte to string
            var bufferedStr = Encoding.UTF8.GetString(bufRecv);

            // we only deal with GET type for the moment
            if (bufferedStr.Substring(0, 3) != "GET")
            {
                Console.WriteLine("Only Get Method is supported..");
                Close();
                return false;
            }

            // Looks for HTTP request
            var iStartPos = bufferedStr.IndexOf("HTTP", 1, StringComparison.Ordinal);

            // Gets the HTTP text and version
            ClientHttpVersion = bufferedStr.Substring(iStartPos, 8);

            Console.WriteLine($"{ClientHttpVersion}");

            return true;
        }

        public void ReplyToInitRequest()
        {
            // TODO send the player to the browser

            SendToBrowser("Ack");

            //SendData(new byte[] { }, 0, 0);
        }

        public void WaitForBufferRequest()
        {
            var bufRecv = new byte[1024];
            while (true)
            {
                Socket.Receive(bufRecv, bufRecv.Length, 0);

                // converts byte to string
                var bufferedStr = Encoding.UTF8.GetString(bufRecv);

                // we only deal with GET type for the moment
                if (bufferedStr.Substring(0, 3) != "GET")
                {
                    Console.WriteLine("Only Get Method is supported..");
                    return;
                }

                if (bufferedStr.Contains("NextBuffer"))
                {
                    break;
                }
            }
        }

        public bool SendData(byte[] buf, int offset, int length)
        {
            SendHeader(length, " 200 OK");
            SendToBrowser(buf, offset, length);
            return true;
        }

        private void SendHeader(int totalBytes, string statusBytes)
        {
            var sBuffer = "";

            sBuffer = sBuffer + ClientHttpVersion + statusBytes + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + MimeType + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            if (totalBytes >= 0)
            {
                sBuffer = sBuffer + "Content-Length: " + totalBytes + "\r\n\r\n";
            }

            Console.WriteLine("=== header ===");
            Console.WriteLine(sBuffer);
            Console.WriteLine("==============");

            SendToBrowser(sBuffer);
        }

        /// <summary>
        ///  Overloaded Function, takes string, convert to bytes and calls 
        ///  overloaded sendToBrowserFunction.
        /// </summary>
        /// <param name="sData">The data to be sent to the browser(client)</param>
        /// <param name="socket">Socket reference</param>
        public bool SendToBrowser(string sData)
        {
            return SendToBrowser(Encoding.UTF8.GetBytes(sData));
        }

        public bool SendToBrowser(byte[] buffer)
        {
            return SendToBrowser(buffer, 0, buffer.Length);
        }

        /// <summary>
        ///  Sends data to the browser (client)
        /// </summary>
        /// <param name="buffer">Byte Array</param>
        /// <param name="offset">The position in the data buffer at which to begin sending data</param>
        /// <param name="length">The number of bytes to send</param>
        public bool SendToBrowser(byte[] buffer, int offset, int length)
        {
            try
            {
                if (Socket.Connected)
                {
                    if ((Socket.Send(buffer, offset, length, 0)) == -1)
                    {
                        Console.WriteLine("Socket Error cannot Send Packet");
                    }
                    else
                    {
                        Console.Write($"S{offset},{length}");
                        //Console.WriteLine("No. of bytes sent {0}", numBytes);
                    }
                    return true;
                }
                Console.WriteLine("Connection Dropped....");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred: {0} ", e);
            }
            return false;
        }
    }
}
