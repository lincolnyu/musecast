using System;
using System.Net.Sockets;
using System.Text;

namespace MuseCastLib
{
    public class MuseTcpSession : MuseBaseSession
    {
        public MuseTcpSession(Socket socket, string mime) : base(socket)
        {
            Mime = mime;
        }

        public string Mime { get; }

        #region ISession members

        public override bool Handshake()
        {
            if (! WaitForInitRequest())
            {
                return false;
            }

            ReplyToInitRequest();
            return true;
        }

        public override void WaitForBufferRequest()
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
                    continue;
                }

                break;
            }
        }

        public override bool SendData(byte[] buf, int offset, int length)
        {
            SendHeader(length, Mime, SuccessStatusMessage);
            SendToBrowser(buf, offset, length);
            return true;
        }

        #endregion

        private bool WaitForInitRequest()
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

        private void ReplyToInitRequest()
        {
            // TODO send the player to the browser
            Console.WriteLine("send acknowledgement");
            SendString("Ack");
            Console.WriteLine("send acknowledgement done");

            //SendData(new byte[] { }, 0, 0);
        }
    }
}
