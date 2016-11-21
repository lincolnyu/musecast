using System.Net;
using System.Net.Sockets;

namespace MuseCastLib
{
    public class MuseTcpListener : TcpListener, IListener
    {
        public const string DefaultMimeType = "audio/mpeg";

        public MuseTcpListener(IPEndPoint localEP, string mimeType = DefaultMimeType) : base(localEP) { MimeType = mimeType; }
        public MuseTcpListener(IPAddress localaddr, int port, string mimeType = DefaultMimeType) : base(localaddr, port) { MimeType = mimeType; }

        public string MimeType { get; }        

        public ISession AcceptNewSession()
        {
            var socket = AcceptSocket();
            return new MuseTcpSession(socket, MimeType);
        }
    }
}
