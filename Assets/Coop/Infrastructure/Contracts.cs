using System.Threading.Tasks;

namespace Coop.Infrastructure
{
    public interface IConnectionTransport
    {
        Task PrepareAsync(bool isHost, string address, ushort port, int maxPlayers);
        ulong GetRtt(ulong clientId);
        bool ValidateIdentity(ulong clientId, string playerId);
        void Tick();
        void SetSessionState(bool inGame, int players, int maxPlayers);
        void Shutdown();
    }

    public interface IPlayerIdentity
    {
        string PlayerId
        {
            get;
        }
        string DisplayName
        {
            get;
        }
    }

    public interface IGameStats
    {
        void RecordThrow();
        void Flush();
    }
}
