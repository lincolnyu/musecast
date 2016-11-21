using System;
using System.Net.Sockets;
using System.Text;

namespace MuseCastLib
{
    public abstract class MuseBaseSession : ISession
    {
        public const string SuccessStatusMessage = " 200 OK";

        public MuseBaseSession(Socket socket)
        {
            Socket = socket;
        }

        #region ISession members

        public virtual bool Started => Socket.Connected;

        #endregion

        public string ClientHttpVersion { get; set; }

        public Socket Socket { get; private set; }

        #region ISession members

        #region IDisposable members

        public virtual void Dispose() => Close();

        #endregion

        public virtual void Close()
        {
            if (Socket != null)
            {
                Socket.Close();
                Socket = null;
            }
        }

        public abstract bool Handshake();

        public abstract void WaitForBufferRequest();

        public abstract bool SendData(byte[] buf, int offset, int length);

        #endregion

        public void SendString(string s, string mime = "text/html")
        {
            var b = Encoding.UTF8.GetBytes(s);
            SendHeader(b.Length, mime, SuccessStatusMessage);
            SendToBrowser(b);
        }

        protected void SendHeader(int totalBytes, string mime, string statusBytes)
        {
            var sBuffer = "";

            sBuffer = sBuffer + ClientHttpVersion + statusBytes + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + mime + "\r\n";
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
        protected bool SendToBrowser(string sData)
        {
            return SendToBrowser(Encoding.UTF8.GetBytes(sData));
        }

        protected bool SendToBrowser(byte[] buffer)
        {
            return SendToBrowser(buffer, 0, buffer.Length);
        }

        /// <summary>
        ///  Sends data to the browser (client)
        /// </summary>
        /// <param name="buffer">Byte Array</param>
        /// <param name="offset">The position in the data buffer at which to begin sending data</param>
        /// <param name="length">The number of bytes to send</param>
        protected bool SendToBrowser(byte[] buffer, int offset, int length)
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
