using System.IO;

namespace NAudioTest
{
    class Program
    {
        public static byte[] WavToMP3(byte[] wavFile)
        {
            using (var source = new MemoryStream(wavFile))
            using (var rdr = new NAudio.Wave.WaveFileReader(source))
            {
                var fmt = new WaveLib.WaveFormat(rdr.WaveFormat.SampleRate, rdr.WaveFormat.BitsPerSample, rdr.WaveFormat.Channels);

                // convert to MP3 at 96kbit/sec...
                var conf = new Yeti.Lame.BE_CONFIG(fmt, 96);

                // Allocate a 1-second buffer
                int blen = rdr.WaveFormat.AverageBytesPerSecond;
                var buffer = new byte[blen];

                // Do conversion
                using (var output = new MemoryStream())
                {
                    var mp3 = new Yeti.MMedia.Mp3.Mp3Writer(output, fmt, conf);

                    int readCount;
                    while ((readCount = rdr.Read(buffer, 0, blen)) > 0)
                        mp3.Write(buffer, 0, readCount);

                    mp3.Close();
                    return output.ToArray();
                }
            }
        }

        static void Main(string[] args)
        {
            var wavInFile = args[0];
            var mp3OutFile = args[1];
            byte[] inbuf;
            using (var wavIn = new FileStream(wavInFile, FileMode.Open))
                using (var wavInBr = new BinaryReader(wavIn))
            {
                var size = (int)wavIn.Length;
                inbuf = wavInBr.ReadBytes(size);
            }
            var outbuf = WavToMP3(inbuf);
            using (var mp3Out = new FileStream(mp3OutFile, FileMode.Create))
            {
                mp3Out.Write(outbuf, 0, outbuf.Length);
            }
        }
    }
}
