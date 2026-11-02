using System;
using UnityEngine;

namespace Coop.Gameplay
{
    // Explicit dependencies supplied by the application's composition root.
    public sealed class GameplayServices
    {
        public GameObject ItemPrefab { get; }
        public Func<ulong, bool> CanPlay { get; }
        public Action<string> Report { get; }
        public Action<ulong> ItemThrown { get; }

        public GameplayServices(GameObject itemPrefab, Func<ulong, bool> canPlay,
            Action<string> report, Action<ulong> itemThrown)
        {
            ItemPrefab = itemPrefab;
            CanPlay = canPlay ?? throw new ArgumentNullException(nameof(canPlay));
            Report = report ?? (_ => { });
            ItemThrown = itemThrown ?? (_ => { });
        }
    }
}
