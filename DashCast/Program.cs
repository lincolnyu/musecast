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
            public int Length => (int)(Start - End + 1);
        }

        private static void GetDashInfo(string mpd, out string mfile, out List<Segment> segments)
        {
            segments = new List<Segment>();
            mfile = null;
            using (var xml = XmlReader.Create(mpd))
            {
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.Element)
                    {
                        if (xml.Value == "BaseURL")
                        {
                            xml.MoveToContent();
                            mfile = xml.Value;
                        }
                        else if (xml.Value == "SegmentURL")
                        {
                            var range = xml.GetAttribute("mediaRange");
                            var s = range.Split('-');
                            var starts = s[0];
                            var ends = s[1];
                            var start = long.Parse(starts);
                            var end = long.Parse(ends);
                            segments.Add(new Segment { Start = start, End = end });
                        }
                    }
                }
            }
        }

        private static void FeedDash(IListener listener, string mfilename, List<Segment> segments)
        {
            using (var mfile = new FileStream(mfilename, FileMode.Open))
                using (var s = new MulticastStream(listener))
            {
                foreach (var segment in segments)
                {
                    var segbuf = new byte[segment.Length];
                    var read = mfile.Read(segbuf, 0, segment.Length);
                    s.Write(segbuf, 0, read);
                }
            }
        }

        static void Main(string[] args)
        {
            var mpd = args[0];
            var ip = args[1];

            string mfile;
            List<Segment> segments;
            GetDashInfo(mpd, out mfile, out segments);

            IPAddress ipAddress;
            int? port;
            NetHelper.ParseIpAddress(ip, out ipAddress, out port);
            if (port == null) port = DefaultPort;
            var listener = new MuseTcpListener(ipAddress, port.Value, "video/mp4");
            FeedDash(listener, mfile, segments);
        }
    }
}
