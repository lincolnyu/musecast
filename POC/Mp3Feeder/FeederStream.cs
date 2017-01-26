using MuseCastLib;
using System.Threading;

namespace Mp3Feeder
{
    public class FeederStream : OnDemandStream
    {
        private long _length;

        private ManualResetEvent _canWriteEvent = new ManualResetEvent(false);
        private ISession _session;

        public FeederStream(IListener listener)
            : base(listener)
        {
        }
        
        public override void SetLength(long value)
        {
            _length = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _canWriteEvent.WaitOne();
            System.Diagnostics.Debug.Assert(_session != null);
            _session.SendData(buffer, offset, count);
        }

        protected override void Stream(ISession session)
        {
            if (!session.Handshake())
            {
                return;
            }
            _session = session;
            _canWriteEvent.Set();
        }
    }
}
