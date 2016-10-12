namespace MuseCast
{
    public interface IListener
    {
        void Start();

        void Stop();

        ISession AcceptNewSession();
    }
}
