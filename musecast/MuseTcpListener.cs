using System.Net;
using System.Net.Sockets;

namespace MuseCast
{
    public class MuseTcpListener : TcpListener, IListener
    {
        public MuseTcpListener(IPEndPoint localEP) : base(localEP) { }
        public MuseTcpListener(IPAddress localaddr, int port) : base(localaddr, port) {}

        public ISession AcceptNewSession()
        {
            var socket = AcceptSocket();
            return new MuseTcpSession(socket);
        }
    }
}
