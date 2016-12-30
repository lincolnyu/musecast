using QSharp.Scheme.Buffering;
using System;
using System.IO;
using System.Threading;

namespace MuseCastLib
{
    public class MulticastStream : Stream
    {
        #region Delegate

        public delegate void InitDataEventHandler(out byte[] buffer, out int start, out int len);

        #endregion

        #region Constants

        private const int DefaultHookCount = 32;
      
        #endregion

        #region Backing fields

        private long _length;

        #endregion

        public int AudioBufferFrameCount => _audioBuffer.HookCount;

        private HookyCircularBuffer _audioBuffer;
       
        private readonly IListener _listener;
        private int _numRunningThreads;
        private readonly AutoResetEvent _doneEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _writtenEvent = new AutoResetEvent(false);
        private bool _terminating = false;

        public MulticastStream(IListener listener, int hookCount = DefaultHookCount)
        {
            try
            {
                _audioBuffer = new HookyCircularBuffer(hookCount);

                //start listing on the given port
                _listener = listener;
                _listener.Start();
                Console.WriteLine("Mp3MultiCastStream listening... Press ^C to stop...");

                //start the thread which calls the method 'StartListen'
                Fork();
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred while listening: " + e);
                throw e;
            }
        }

        ~MulticastStream()
        {
            TerminateAllThreadsIfNot();
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get; set;
        }

        public bool InitDataCombinedWithFirstChunk { get; set; } = false;

        public event InitDataEventHandler InitData;

        public override void Flush()
        {
            
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Close()
        {
            base.Close();

            TerminateAllThreadsIfNot();
        }

        private void TerminateAllThreadsIfNot()
        {
            if (_listener != null)
            {
                _terminating = true;
                _listener.Stop();
                while (_numRunningThreads > 0)
                {
                    _doneEvent.WaitOne();
                }
                Console.WriteLine("All threads quited nicely.");
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var buf = new byte[count];
            var j = 0;
            for (var i = offset; i < count; i++, j++)
            {
                buf[j] = buffer[i];
            }
            _audioBuffer.Hook(buf);
            
            _writtenEvent.Set();
        }

        private void TryStartListen(object obj)
        {
            ISession session = null;
            var forked = false;
            try
            {
                do
                {
                    // accepts a new connection
                    session = _listener.AcceptNewSession();
                } while (!session.Started && !_terminating);

                if (!_terminating)
                {
                    forked = Fork();
                    Stream(session);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error occurred: {0} ", e);
            }
            finally
            {
                session?.Close();
                Console.WriteLine("Socket closed");
                if (forked)
                {
                    Merge();
                }
            }
        }

        private bool Fork()
        {
            var succ = ThreadPool.QueueUserWorkItem(TryStartListen);
            if (succ)
            {
                Interlocked.Increment(ref _numRunningThreads);
            }
            return succ;
        }

        private void Merge()
        {
            Interlocked.Decrement(ref _numRunningThreads);
            _doneEvent.Set();
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
        private void Stream(ISession session)
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
            while (!_terminating && !error)
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
