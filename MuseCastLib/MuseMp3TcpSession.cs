using System;
using System.Net.Sockets;
using System.Text;

namespace MuseCastLib
{
    public class MuseMp3TcpSession : MuseBaseSession
    {
        public const int LongLength = 1024 * 1024 * 1024;

        private bool _sendHead = true;

        public MuseMp3TcpSession(Socket socket, string mime = "audio/mpeg") : base(socket)
        {
            Mime = mime;
        }

        public string Mime { get; }

        public override bool Handshake()
        {
            if (!ProcessInitRequest()) return false;
            _sendHead = true;
            return true;
        }
        
        public override void WaitForBufferRequest()
        {
        }

        public override bool SendData(byte[] buf, int offset, int length)
        {
            if (_sendHead)
            {
                SendHeader(LongLength, Mime, SuccessStatusMessage);
                _sendHead = false;
            }
            SendToBrowser(buf, offset, length);
            return true;
        }

        private bool ProcessInitRequest()
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
    }
}
