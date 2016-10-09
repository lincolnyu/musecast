﻿using System;
using System.Net;
using NAudio.Lame;
using NAudio.Wave;
using static MuseCast.AudioCapture;
using System.Runtime.InteropServices;
using System.Linq;
using System.IO;

namespace MuseCast
{
    class Program
    {
        const int DefaultPort = 9000;

        private static void ParseIpAddress(string address, out IPAddress ipAddress, out int port)
        {
            var s1 = address.Split(':');
            string addressStr;
            if (s1.Length == 2)
            {
                addressStr = s1[0];
                var portStr = s1[1];
                if (!int.TryParse(portStr, out port)) port = DefaultPort;
            }
            else if (s1.Length == 1)
            {
                addressStr = address;
                port = DefaultPort;
            }
            else
            {
                throw new ArgumentException("Wrong IP address format");
            }

            var s2 = addressStr.Split('.');
            if (s2.Length != 4)
            {
                throw new ArgumentException("Wrong IP address format");
            }
            var ipComponents = s2.Select(x => byte.Parse(x)).ToArray();
            ipAddress = new IPAddress(ipComponents);
        }

        private static void WriteMp3NAudio(string address)
        {
            LameMP3FileWriter writer = null;
            try
            {
                IPAddress ipAddress;
                int port;
                ParseIpAddress(address, out ipAddress, out port);
                using (var stream = new Mp3MulticastStream(ipAddress, port))
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
                int port;
                int inbps = 16;
                ParseIpAddress(address, out ipAddress, out port);
                using (var stream = new Mp3MulticastStream(ipAddress, port))
                {
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

                        writer.Write(convertedData, 0, convertedData.Length);
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
        
        static void Main(string[] args)
        {
            WriteMp3Yeti(args[0]);
        }
    }
}
