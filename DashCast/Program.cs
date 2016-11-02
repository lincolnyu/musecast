using MuseCastLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace DashCast
{
    public class Program
    {
        const int DefaultPort = 9000;

        class Segment
        {
            public long Start { get; set; }
            public long End { get; set; }
            public int Length => (int)(End - Start + 1);
        }

        private static void GetDashInfo(string mpd, out string mfile, out Segment initSeg, out List<Segment> segments)
        {
            initSeg = null;
            segments = new List<Segment>();
            mfile = null;
            using (var xml = XmlReader.Create(mpd))
            {
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.Element)
                    {
                        if (xml.Name == "BaseURL")
                        {
                            mfile = xml.ReadElementContentAsString();
                        }
                        else if (xml.Name == "Initialization")
                        {
                            var range = xml.GetAttribute("range");
                            initSeg = LoadSegmentFromRangeString(range);
                        }
                        else if (xml.Name == "SegmentURL")
                        {
                            var range = xml.GetAttribute("mediaRange");
                            var seg = LoadSegmentFromRangeString(range);
                            segments.Add(seg);
                        }
                    }
                }
            }
        }

        private static Segment LoadSegmentFromRangeString(string range)
        {
            var s = range.Split('-');
            var starts = s[0];
            var ends = s[1];
            var start = long.Parse(starts);
            var end = long.Parse(ends);
            return new Segment { Start = start, End = end };
        }

        private static string WaitOnGet(Socket s)
        {
            string bufferedStr;
            do
            {
                var bufRecv = new byte[1024];
                s.Receive(bufRecv, bufRecv.Length, 0);
                // converts byte to string
                bufferedStr = Encoding.UTF8.GetString(bufRecv);
            } while (bufferedStr.Substring(0, 3) != "GET");

            return bufferedStr;
        }

        private static void FeedDashAsDash(MuseTcpListener listener, string mfilename, Segment initSeg, List<Segment> segments)
        {
            listener.Start();
            using (var session = (MuseTcpSession)listener.AcceptNewSession())
            {
                var s = session.Socket;
                var req = WaitOnGet(s);
                // Looks for HTTP request
                var iStartPos = req.IndexOf("HTTP", 1, StringComparison.Ordinal);
                session.ClientHttpVersion = req.Substring(iStartPos, 8);

                Console.WriteLine("sending ack");

                session.SendString("Ack");

                WaitOnGet(s);

                using (var mfile = new FileStream(mfilename, FileMode.Open))
                {
                    mfile.Seek(0, SeekOrigin.Begin);
                    var iniSegBuf = new byte[initSeg.Length];
                    var iniRead = mfile.Read(iniSegBuf, 0, initSeg.Length);
                    var ftt = true;

                    foreach (var seg in segments)
                    {
                        if (ftt)
                        {
                            var segbuf = new byte[iniRead + seg.Length];
                            for (var i = 0; i < iniRead; i++)
                            {
                                segbuf[i] = iniSegBuf[i];
                            }
                            var read = mfile.Read(segbuf, iniRead, seg.Length);
                            session.SendData(segbuf, 0, iniRead + read);
                            ftt = false;
                        }
                        else
                        {
                            var segbuf = new byte[seg.Length];
                            var read = mfile.Read(segbuf, 0, seg.Length);
                            session.SendData(segbuf, 0, read);
                        }

                        WaitOnGet(s);
                    }
                }
            }
        }
        
        private static void FeedDashAsLive(IListener listener, string mfilename, Segment initSeg, List<Segment> segments)
        {
            using (var mfile = new FileStream(mfilename, FileMode.Open))
                using (var s = new MulticastStream(listener) { InitDataCombinedWithFirstChunk = true } )
            {
                s.InitData += (out byte[] buffer, out int start, out int len) =>
                {
                    mfile.Seek(0, SeekOrigin.Begin);
                    buffer = new byte[initSeg.Length];
                    start = 0;
                    len = mfile.Read(buffer, 0, initSeg.Length);
                };
                var dataPos = mfile.Position;
                while (true) // endless loop
                {
                    mfile.Seek(dataPos, SeekOrigin.Begin);
                    foreach (var seg in segments)
                    {
                        var segbuf = new byte[seg.Length];
                        var read = mfile.Read(segbuf, 0, seg.Length);
                        s.Write(segbuf, 0, read);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            var mpd = args[0];
            var ip = args[1];

            string mfile;
            Segment initSeg;
            List<Segment> segments;
            GetDashInfo(mpd, out mfile, out initSeg, out segments);

            IPAddress ipAddress;
            int? port;
            NetHelper.ParseIpAddress(ip, out ipAddress, out port);
            if (port == null) port = DefaultPort;
            var listener = new MuseTcpListener(ipAddress, port.Value, "video/mp4");
            //FeedDashAsLive(listener, mfile, initSeg, segments);
            FeedDashAsDash(listener, mfile, initSeg, segments);
        }
    }
}
