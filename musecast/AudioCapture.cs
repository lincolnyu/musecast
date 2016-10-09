using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MuseCast
{
    public static class AudioCapture
    {
        [StructLayout(LayoutKind.Sequential, Size = 18)]
        public class WAVEFORMATEX
        {
            public ushort wFormatTag;       /* format type */
            public ushort nChannels;        /* number of channels (i.e. mono, stereo...) */
            public uint nSamplesPerSec;    /* sample rate */
            public uint nAvgBytesPerSec;   /* for buffer estimation */
            public ushort nBlockAlign;      /* block size of data */
            public ushort wBitsPerSample;   /* Number of bits per sample of mono data */
            public ushort cbSize;           /* The count in bytes of the size of
                                               extra information (after cbSize) */

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"wFormatTag = {wFormatTag}");
                sb.AppendLine($"nChannels = {nChannels}");
                sb.AppendLine($"nSamplesPerSec = {nSamplesPerSec}");
                sb.AppendLine($"nAvgBytesPerSec = {nAvgBytesPerSec}");
                sb.AppendLine($"nBlockAlign = {nBlockAlign}");
                sb.AppendLine($"wBitsPerSample = {wBitsPerSample}");
                sb.AppendLine($"cbSize = {cbSize}");
                return sb.ToString();
            }
        }

        public delegate int SetFormatCallback(WAVEFORMATEX pwf);
        
        public delegate int CopyDataCallback(IntPtr pData, uint numFramesAvailable, [MarshalAs(UnmanagedType.Bool)] ref bool done);

        [DllImport("AudioCapture.dll")]
        public extern static int RecordAudioStream(SetFormatCallback setFormat, CopyDataCallback copyData);

        [DllImport("AudioCapture.dll", EntryPoint = "Test")]
        public extern static void Test();
    }
}
