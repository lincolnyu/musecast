using System;
using System.Net;
using NAudio.Lame;
using NAudio.Wave;
using static MuseCast.AudioCapture;
using System.Runtime.InteropServices;

namespace MuseCast
{
    class Program
    {
        static void Main(string[] args)
        {
            const int DefaultPort = 9000;
            int port = DefaultPort;
            if (args.Length== 1)
            {
                if (!int.TryParse(args[0], out port))
                {
                    port = DefaultPort;
                }
            }
            LameMP3FileWriter writer = null;
            try
            {
                using (var stream = new Mp3MulticastStream(new IPAddress(new byte[] { 192, 168, 1, 3 }), port))
                {
                    // how to use lame:
                    // http://stackoverflow.com/questions/23441298/how-can-i-save-a-music-network-stream-to-a-mp3-file
                    RecordAudioStream((WAVEFORMATEX pwfx) =>
                    {
                        var fmt = new WaveFormat((int)pwfx.nSamplesPerSec, pwfx.nChannels);
                        Console.WriteLine($"format got = {pwfx}");
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
    }
}
