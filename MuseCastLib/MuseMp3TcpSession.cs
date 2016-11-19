using System.Net.Sockets;

namespace MuseCastLib
{
    public class MuseMp3TcpSession : MuseBaseSession
    {
        public const int LongLength = 1024 * 1024 * 1024;

        public MuseMp3TcpSession(Socket socket, string mimeType) : base(socket, mimeType)
        {
        }

        public override bool Handshake()
        {
            SendHeader(LongLength, SuccessStatusMessage);
            return true;
        }
        
        public override void WaitForBufferRequest()
        {
        }

        public override bool SendData(byte[] buf, int offset, int length)
        {
            SendToBrowser(buf, offset, length);
            return true;
        }
    }
}
