using MuseCastLib;
using System.Collections.Generic;
using System.IO;
using System.Net;
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

        private static void FeedDash(IListener listener, string mfilename, Segment initSeg, List<Segment> segments)
        {
            using (var mfile = new FileStream(mfilename, FileMode.Open))
                using (var s = new MulticastStream(listener) { InitDataCombinedWithFirstChunk = true } )
            {
                s.InitData += (out byte[] buffer, out int start, out int len) =>
                {
                    var segbuf = new byte[initSeg.Length];
                    mfile.Seek(0, SeekOrigin.Begin);
                    buffer = new byte[initSeg.Length];
                    start = 0;
                    len = mfile.Read(segbuf, 0, initSeg.Length);
                };
                var dataPos = mfile.Position;
                while (true) // endless loop
                {
                    mfile.Seek(dataPos, SeekOrigin.Begin);
                    foreach (var segment in segments)
                    {
                        var segbuf = new byte[segment.Length];
                        var read = mfile.Read(segbuf, 0, segment.Length);
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
            FeedDash(listener, mfile, initSeg, segments);
        }
    }
}
