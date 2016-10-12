using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MuseCast
{
    public class Mp3MulticastStream : Stream
    {
        #region Constants

        private const int DefaultAudioBufferFrameCount = 32;
        private const string HttpVersion = "1.1";
        
        #endregion

        #region Backing fields

        private long _length;

        #endregion

        byte[][] _audioBuffers;
        ReaderWriterLock[] _bufferLocks;
        int _currentWriting = 0;

        private readonly IListener _listener;
        private int _numRunningThreads;
        private readonly AutoResetEvent _doneEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _writtenEvent = new AutoResetEvent(false);
        private bool _terminating = false;

        public Mp3MulticastStream(IListener listenr, int audioBufferFrameCount = DefaultAudioBufferFrameCount)
        {
            try
            {
                _audioBuffers = new byte[audioBufferFrameCount][];
                _bufferLocks = new ReaderWriterLock[audioBufferFrameCount];
                for (var i = 0; i < _bufferLocks.Length; i++)
                {
                    _bufferLocks[i] = new ReaderWriterLock();
                }

                //start listing on the given port
                _listener = listenr;
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

        ~Mp3MulticastStream()
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

        private void Stream(ISession session)
        {
            if (!session.WaitForInitRequest())
            {
                return;
            }

            session.ReplyToInitRequest();

            int currentReading = 0;
            var error = false;
            while (!_terminating && !error)
            {
                session.WaitForBufferRequest();

                while (currentReading == _currentWriting)
                {
                    _writtenEvent.WaitOne();
                }

                _bufferLocks[currentReading].AcquireReaderLock(-1);
                var buf = _audioBuffers[currentReading];
                error = !session.SendData(buf, 0, buf.Length);
                _bufferLocks[currentReading].ReleaseReaderLock();
                currentReading = (currentReading + 1) % _bufferLocks.Length;
            }
        }
    }
}
