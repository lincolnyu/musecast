namespace MuseCastLib
{
    public interface ISession
    {
        bool Started { get; }

        void Close();

        bool Handshake();
        void WaitForBufferRequest();
        bool SendData(byte[] buf, int offset, int length);
    }
}
