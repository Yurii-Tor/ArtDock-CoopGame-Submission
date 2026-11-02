using System.Collections.Generic;

namespace Coop.Session
{
    // Initial scene loading waits for the participating clients. Once opened,
    // subsequent late joiners synchronize independently and cannot close it.
    public sealed class LoadingBarrier
    {
        private readonly HashSet<ulong> participants = new HashSet<ulong>();
        private readonly HashSet<ulong> pending = new HashSet<ulong>();
        public bool IsLoading
        {
            get; private set;
        }

        public void Begin(IEnumerable<ulong> clients)
        {
            Reset();
            IsLoading = true;
            foreach (ulong client in clients)
                Add(client);
        }

        public void Add(ulong client)
        {
            if (IsLoading && participants.Add(client))
                pending.Add(client);
        }

        public void MarkReady(ulong client) => pending.Remove(client);

        public void Remove(ulong client)
        {
            participants.Remove(client);
            pending.Remove(client);
        }

        public bool TryOpen()
        {
            if (!IsLoading || participants.Count == 0 || pending.Count != 0)
                return false;
            IsLoading = false;
            return true;
        }

        public void Reset()
        {
            IsLoading = false;
            participants.Clear();
            pending.Clear();
        }
    }
}
