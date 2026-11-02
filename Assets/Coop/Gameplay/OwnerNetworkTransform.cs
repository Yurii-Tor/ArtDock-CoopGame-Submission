using Unity.Netcode.Components;

namespace Coop.Gameplay
{
    // Responsive local movement; NGO interpolates the other players' snapshots.
    // This intentionally does not claim authoritative movement anti-cheat.
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
