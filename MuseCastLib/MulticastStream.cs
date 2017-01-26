using QSharp.Scheme.Buffering;
using System;
using System.IO;
using System.Threading;

namespace MuseCastLib
{
    public class MulticastStream : OnDemandStream
    {
        #region Delegate

        public delegate void InitDataEventHandler(out byte[] buffer, out int start, out int len);

        #endregion

        #region Constants

        private const int DefaultHookCount = 32;

        #endregion

        public int AudioBufferFrameCount => _audioBuffer.HookCount;

        private HookyCircularBuffer _audioBuffer;
       
        public MulticastStream(IListener listener, int hookCount = DefaultHookCount) : base(listener)
        {
            _audioBuffer = new HookyCircularBuffer(hookCount);
        }

        public event InitDataEventHandler InitData;

        public override void Write(byte[] buffer, int offset, int count)
        {
            var buf = new byte[count];
            var j = 0;
            for (var i = offset; i < count; i++, j++)
            {
                buf[j] = buffer[i];
            }
            _audioBuffer.Hook(buf);
            // TODO event/notification?
        }
        
        /// <summary>
        ///  The entire broadcast streaming process for the session
        /// </summary>
        /// <param name="session">The session</param>
        /// <remarks>
        ///    Client         Server (this)
        ///   
        ///        InitRequet
        ///      --------------&gt;
        ///      
        ///        ReplyToInitReq
        ///     &lt;--------------
        ///     
        ///        BufferRequest
        ///      --------------&gt;
        ///      
        ///        InitData / InitData+ContentData
        ///     &lt;--------------
        ///     
        ///        BufferRequest
        ///      --------------&gt;
        ///      
        ///        ContentData
        ///     &lt;--------------
        ///     
        ///        ...
        /// 
        /// </remarks>
        protected override void Stream(ISession session)
        {
            if (!session.Handshake())
            {
                return;
            }

            var inited = false;
            // TODO consider registered reader, which may block writer?
            var reader = new HookyCircularBuffer.Reader(_audioBuffer);
            _audioBuffer.RecommendReader(reader);
            var error = false;
            while (!Terminating && !error)
            {
                byte[] initData = null;
                int initDataStart = 0, initDataLen = 0;

                session.WaitForBufferRequest();

                if (!inited && InitData != null)
                {
                    InitData(out initData, out initDataStart, out initDataLen);
                    inited = true;
                }

                if (!InitDataCombinedWithFirstChunk && initData != null)
                {
                    error = !session.SendData(initData, initDataStart, initDataLen);
                    if (error)
                    {
                        break;
                    }
                    continue;
                }

                var readlen = _audioBuffer.RecommendReadLen2(reader);
                var buf = new byte[readlen];
                var read = reader.Read(buf, 0, readlen);
                if (buf != null && read > 0)
                {
                    error = !session.SendData(buf, 0, read);
                }
            }
        }

        private void BufCopy(byte[] dst, byte[] src, int offsetDst = 0) => BufCopy(dst, src, offsetDst, 0, src.Length);

        private void BufCopy(byte[] dst, byte[] src, int offsetDst, int offsetSrc, int len)
        {
            var pdst = offsetDst;
            for (var i = offsetSrc; i < offsetSrc + len; i++)
            {
                dst[pdst++] = src[i];
            }
        }
    }
}
