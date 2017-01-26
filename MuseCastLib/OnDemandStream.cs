using System;
using System.IO;
using System.Threading;

namespace MuseCastLib
{
    public abstract class OnDemandStream : Stream
    {
        private readonly IListener _listener;
        private int _numRunningThreads;
        private readonly AutoResetEvent _doneEvent = new AutoResetEvent(false);
        protected bool Terminating { get; private set; }  = false;

        protected OnDemandStream(IListener listener)
        {
            try
            {
                //start listing on the given port
                _listener = listener;
                _listener.Start();
                Console.WriteLine($"{GetType().Name} listening... Press ^C to stop...");

                //start the thread which calls the method 'StartListen'
                Fork();
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred while listening: " + e);
                throw e;
            }
        }

        ~OnDemandStream()
        {
            TerminateAllThreadsIfNot();
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => long.MaxValue; // TODO makes sense?

        public override long Position
        {
            get; set;
        }

        public bool InitDataCombinedWithFirstChunk { get; set; } = false;

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

        protected abstract void Stream(ISession session);

        private void TerminateAllThreadsIfNot()
        {
            if (_listener != null)
            {
                Terminating = true;
                _listener.Stop();
                while (_numRunningThreads > 0)
                {
                    _doneEvent.WaitOne();
                }
                Console.WriteLine("All threads quited nicely.");
            }
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
                } while (!session.Started && !Terminating);

                if (!Terminating)
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
    }
}
