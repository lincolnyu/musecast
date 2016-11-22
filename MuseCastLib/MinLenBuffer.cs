using System;

namespace MuseCastLib
{
    public class MinLenBuffer
    {
        private byte[] _buf;
        private byte[] _nextBuf;
        private int _pt;

        public MinLenBuffer(int len)
        {
            MinLen = len;
            _buf = new byte[MinLen];
        }

        public byte[] Buffer => _buf;
        public int MinLen { get; }
        public bool BufferReady { get; private set; }

        public void Write(byte[] buf, int offset, int size)
        {
            int i;
            for (i = offset; i < offset+size && _pt < _buf.Length; i++)
            {
                _buf[_pt++] = buf[i];
            }
            if (_pt == _buf.Length)
            {
                BufferReady = true;
                _pt = 0;
                var reqsize = offset + size - i;
                _nextBuf = new byte[Math.Max(reqsize, MinLen)];
                for (;  i < offset + size; i++)
                {
                    _nextBuf[_pt++] = buf[i];
                }
            }
        }

        public byte[] PopBuffer()
        {
            var tmp = _buf;
            _buf = _nextBuf;
            _nextBuf = null;
            BufferReady = false;
            return tmp;
        }
    }
}
