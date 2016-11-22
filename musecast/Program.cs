using System;
using System.Net;
using NAudio.Lame;
using NAudio.Wave;
using System.Runtime.InteropServices;
using System.IO;
using MuseCastLib;
using static MuseCast.AudioCapture;
using System.Threading;

namespace MuseCast
{
    class Program
    {
        const int DefaultPort = 9000;

        private static void WriteMp3NAudio(string address)
        {
            LameMP3FileWriter writer = null;
            try
            {
                IPAddress ipAddress;
                int? port;
                NetHelper.ParseIpAddress(address, out ipAddress, out port);
                if (port == null) port = DefaultPort;
                var listener = new MuseTcpListener(ipAddress, port.Value);
                using (var stream = new MulticastStream(listener))
                {
                    // how to use lame:
                    // http://stackoverflow.com/questions/23441298/how-can-i-save-a-music-network-stream-to-a-mp3-file
                    RecordAudioStream((WAVEFORMATEX pwfx) =>
                    {
                        var fmt = new WaveFormat((int)pwfx.nSamplesPerSec, pwfx.nChannels);
                        Console.WriteLine($"format obtained = {pwfx}");
                        writer = new LameMP3FileWriter(stream, fmt, LAMEPreset.STANDARD);
                        return 0;
                    },
                    (IntPtr pData, uint numFramesAvailable, ref bool done) =>
                    {
                        // Console.WriteLine($"datalen = {data.Length}");
                        var numBytes = (int)numFramesAvailable;
                        var data = new byte[numBytes];
                        Marshal.Copy(pData, data, 0, numBytes);
                        writer.Write(data, 0, numBytes);
                        return 0;
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred while running MuseCast: " + e);
            }
            finally
            {
                writer?.Dispose();
                writer = null;
            }
        }

        private static void ConvertTo16bps(byte[] inbuf, out byte[] outbuf, int inbps)
        {
            if (inbps == 16)
            {
                outbuf = inbuf;
                return;
            }
            if(inbps == 32)
            {
                const double amp = 1;
                var sampleCount = inbuf.Length / 4;
                outbuf = new byte[sampleCount*2];
                using (var ms = new MemoryStream(inbuf))
                using (var br = new BinaryReader(ms))
                using (var mso = new MemoryStream(outbuf))
                using (var bw = new BinaryWriter(mso))
                {
                    for (var i = 0; i < sampleCount; i++)
                    {
                        var v = br.ReadSingle();
                        var v16 = (short)Math.Round(v * amp);
                        bw.Write(v16);
                    }
                }
                return;
            }
            throw new NotSupportedException();
        }

        private static void WriteMp3Yeti(string address)
        {
            Yeti.MMedia.Mp3.Mp3Writer writer = null;
            try
            {
                IPAddress ipAddress;
                int? port;
                int inbps = 16;
                NetHelper.ParseIpAddress(address, out ipAddress, out port);
                if (port == null) port = DefaultPort;
                var listener = new MuseMp3TcpListener(ipAddress, port.Value);

                var r = new Random();
                using (var stream = new MulticastStream(listener, 10*1024))
                {
#if true

                    // how to use lame:
                    // http://stackoverflow.com/questions/23441298/how-can-i-save-a-music-network-stream-to-a-mp3-file
                    RecordAudioStream((WAVEFORMATEX pwfx) =>
                    {
                        inbps = pwfx.wBitsPerSample;
                        var fmt = new WaveLib.WaveFormat((int)pwfx.nSamplesPerSec, 16, pwfx.nChannels);
                        // convert to MP3 at 128kbit/sec...
                        var conf = new Yeti.Lame.BE_CONFIG(fmt, 128);
                        Console.WriteLine($"format obtained = {pwfx}");
                        writer = new Yeti.MMedia.Mp3.Mp3Writer(stream, fmt, conf);
                        return 0;
                    },
                    (IntPtr pData, uint numFramesAvailable, ref bool done) =>
                    {
                        // Console.WriteLine($"datalen = {data.Length}");
                        var numBytes = (int)numFramesAvailable;
                        var data = new byte[numBytes];
                        Marshal.Copy(pData, data, 0, numBytes);

                        byte[] convertedData;
                        ConvertTo16bps(data, out convertedData, inbps);

                        for (var j = 0; j < convertedData.Length; j++)
                        {
                            convertedData[j] = (byte)r.Next(256);
                        }

                        writer.Write(convertedData, 0, convertedData.Length);

                        return 0;
                    });
#else
                    while (true)
                    {
                        var bufc = 0;
                        using (var inputmp3 = new FileStream(@"d:\temp\mopc15.mp3", FileMode.Open))
                        {
                            const int buflen = 10 * 1024;
                            var b = new byte[buflen];
                            while (true)
                            {
                                var read = inputmp3.Read(b, 0, buflen);
                                if (read > 0)
                                {
                                    stream.Write(b, 0, read);
                                }
                                else
                                {
                                    break;
                                }
                                if(bufc > 0)
                                {
                                    bufc--;
                                }
                                else
                                {
                                    Thread.Sleep(1000);
                                }
                            }
                        }
                    }
#endif
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred while running MuseCast: " + e);
            }
            finally
            {
                writer?.Dispose();
                writer = null;
            }
        }
        
        static void Main(string[] args)
        {
            WriteMp3Yeti(args[0]);
        }
    }
}
