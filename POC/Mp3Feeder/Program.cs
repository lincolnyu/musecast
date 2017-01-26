using MuseCastLib;
using QSharp.Scheme.Buffering;
using System.IO;

namespace Mp3Feeder
{
    class Program
    {
        static void Main(string[] args)
        {
            var fn = args[0];
            var address = args[1];
            var bitrate = double.Parse(args[2]);
            System.Net.IPAddress ipAddress;
            int? port;
            NetHelper.ParseIpAddress(address, out ipAddress, out port);
            if (port == null) port = 8080;
            var listener = new MuseMp3TcpListener(ipAddress, port.Value);

            using (var stream = new FeederStream(listener))
            using (var f = new FileStream(fn, FileMode.Open))
            {
                const int bufsize = 4096;
                var buf = new byte[bufsize];
                while (true)
                {
                    var read = f.Read(buf, 0, bufsize);
                    if (read <= 0) break;
                    stream.Write(buf, 0, read);
                }
            }
        }
    }
}
