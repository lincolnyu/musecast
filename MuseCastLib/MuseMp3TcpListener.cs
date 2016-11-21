using System.Net;
using System.Net.Sockets;

namespace MuseCastLib
{
    public class MuseMp3TcpListener : TcpListener, IListener
    {
        public const string DefaultMimeType = "audio/mpeg";

        public MuseMp3TcpListener(IPEndPoint localEP, string mimeType = DefaultMimeType) : base(localEP) { MimeType = mimeType; }
        public MuseMp3TcpListener(IPAddress localaddr, int port, string mimeType = DefaultMimeType) : base(localaddr, port) { MimeType = mimeType; }

        public string MimeType { get; }

        public ISession AcceptNewSession()
        {
            var socket = AcceptSocket();
            return new MuseMp3TcpSession(socket, MimeType);
        }
    }
}
