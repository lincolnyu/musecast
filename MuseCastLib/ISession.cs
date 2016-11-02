using System;

namespace MuseCastLib
{
    public interface ISession : IDisposable
    {
        bool Started { get; }

        void Close();

        bool Handshake();
        void WaitForBufferRequest();
        bool SendData(byte[] buf, int offset, int length);
    }
}
