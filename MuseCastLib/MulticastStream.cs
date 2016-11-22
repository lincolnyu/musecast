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

        private const int DefaultAudioBufferFrameCount = 32;
      
        #endregion

        #region Backing fields

        private long _length;

        #endregion

        int AudioBufferFrameCount => _audioBuffers.Length;

        byte[][] _audioBuffers;
        ReaderWriterLock[] _bufferLocks;
        int _currentWriting = 0;

        MinLenBuffer _inputBuffer;

        private readonly IListener _listener;
        private int _numRunningThreads;
        private readonly AutoResetEvent _doneEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _writtenEvent = new AutoResetEvent(false);
        private bool _terminating = false;

        public MulticastStream(IListener listener, int inputBufLen = 0, int audioBufferFrameCount = DefaultAudioBufferFrameCount)
        {
            try
            {
                if (inputBufLen > 0)
                {
                    _inputBuffer = new MinLenBuffer(inputBufLen);
                }
                _audioBuffers = new byte[audioBufferFrameCount][];
                _bufferLocks = new ReaderWriterLock[audioBufferFrameCount];
                for (var i = 0; i < _bufferLocks.Length; i++)
                {
                    _bufferLocks[i] = new ReaderWriterLock();
                }

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
            if (_inputBuffer != null)
            {
                _inputBuffer.Write(buffer, offset, count);
                if (_inputBuffer.BufferReady)
                {
                    buffer = _inputBuffer.PopBuffer();
                    offset = 0;
                    count = buffer.Length;
                }
                else
                {
                    return;
                }
            }

            _bufferLocks[_currentWriting].AcquireWriterLock(-1);
            // TODO optimize
            var buf = _audioBuffers[_currentWriting] = new byte[count];
            var j = 0;
            for (var i = offset; i < count; i++, j++)
            {
                buf[j] = buffer[i];
            }
            _bufferLocks[_currentWriting].ReleaseWriterLock();
            _currentWriting++;
            if (_currentWriting >= _bufferLocks.Length) _currentWriting = 0;
            _length += count;
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
            int currentReading = (_currentWriting + AudioBufferFrameCount / 4) % AudioBufferFrameCount;
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

                while (currentReading == _currentWriting)
                {
                    _writtenEvent.WaitOne();
                }

                _bufferLocks[currentReading].AcquireReaderLock(-1);
                byte[] buf;
                if (InitDataCombinedWithFirstChunk && initData != null)
                {
                    buf = new byte[initDataLen + _audioBuffers[currentReading].Length];
                    BufCopy(buf, initData, 0, initDataStart, initDataLen);
                    BufCopy(buf, _audioBuffers[currentReading], initDataLen);
                }
                else
                {
                    buf = _audioBuffers[currentReading];
                }
                if (buf != null && buf.Length > 0)
                {
                    error = !session.SendData(buf, 0, buf.Length);
                }
                _bufferLocks[currentReading].ReleaseReaderLock();
                currentReading = (currentReading + 1) % _bufferLocks.Length;
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
