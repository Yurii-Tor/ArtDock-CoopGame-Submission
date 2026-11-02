using System.Collections;
using System.Reflection;
using Coop.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace Coop.App
{
    // Opt-in integration probe. Uses real RPCs over the selected transport.
    // Launch exactly two processes with --race plus --host / --join.
    public sealed class RaceProbe : MonoBehaviour
    {
        private CompositionRoot root;
        private float deadline;
        private bool finished;

        public void Initialize(CompositionRoot composition)
        {
            root = composition;
            deadline = Time.realtimeSinceStartup + 30;
            StartCoroutine(Run());
        }

        private void Update()
        {
            if (!finished && root != null && Time.realtimeSinceStartup >= deadline)
                Fail("30 second timeout: " + root.Session.Status);
        }

        private IEnumerator Run()
        {
            while (!root.Network.IsConnectedClient)
                yield return null;
            if (root.Network.IsHost)
            {
                while (root.Session.State == null || root.Session.State.Players.Count < 2)
                    yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                root.Session.StartGame();
            }
            while (!root.Session.LocalCanPlay || !PlayerAvatar.Local)
                yield return null;

            var avatar = PlayerAvatar.Local;
            avatar.BotControlled = true;
            avatar.GetComponent<CharacterController>().enabled = false;
            avatar.transform.SetPositionAndRotation(
                new Vector3(root.Network.IsHost ? -0.55f : 0.55f, 0.1f, -5), Quaternion.identity);
            double raceAt = root.Session.State.PlayAt.Value + 5;
            if (root.Network.IsHost)
            {
                while (root.Network.ServerTime.Time < raceAt - 4)
                    yield return null;
                avatar.RequestSpawn();
            }

            PhysicsItem item = null;
            while (!item)
            {
                var items = FindObjectsByType<PhysicsItem>(FindObjectsSortMode.None);
                if (items.Length > 0)
                    item = items[0];
                yield return null;
            }
            if (root.Network.ServerTime.Time > raceAt - 0.5)
            {
                Fail("Item arrived too late to schedule a contested grab.");
                yield break;
            }
            Debug.Log($"RACE_ARMED client={root.Network.LocalClientId} item={item.NetworkObjectId} at={raceAt:F3}");
            while (root.Network.ServerTime.Time < raceAt)
                yield return null;
            avatar.RequestGrab(item.NetworkObjectId);

            while (root.Network.ServerTime.Time < raceAt + 1.5)
                yield return null;
            if (!CheckInvariant(item, avatar, out string failure))
            {
                Fail(failure);
                yield break;
            }
            ulong winner = item.Holder.Value;
            if (winner != root.Network.LocalClientId)
            {
                // Exercise the server's exact-holder check with a real forged request.
                // Reflection stays in this opt-in probe; no unsafe gameplay API is added.
                var interact = typeof(PlayerAvatar).GetMethod("InteractRpc", BindingFlags.Instance | BindingFlags.NonPublic);
                if (interact == null)
                {
                    Fail("Cannot locate interaction RPC for negative test.");
                    yield break;
                }
                interact.Invoke(avatar, new object[] { (byte)3, item.NetworkObjectId, default(RpcParams) });
                Debug.Log($"RACE_UNAUTHORIZED_DELETE_SENT client={root.Network.LocalClientId} item={item.NetworkObjectId}");
            }

            while (root.Network.ServerTime.Time < raceAt + 3)
                yield return null;
            if (!CheckInvariant(item, avatar, out failure) || item.Holder.Value != winner)
            {
                Fail("After unauthorized deletion: " + failure);
                yield break;
            }
            Debug.Log($"RACE_PASS client={root.Network.LocalClientId} winner={winner} item={item.NetworkObjectId} owner={item.OwnerClientId}");
            // Keep the host up long enough for the remote observer to finish its checks.
            while (root.Network.ServerTime.Time < raceAt + (root.Network.IsHost ? 5 : 4))
                yield return null;
            finished = true;
            root.Session.Leave();
            yield return new WaitForSecondsRealtime(0.25f);
            Application.Quit();
        }

        private bool CheckInvariant(PhysicsItem item, PlayerAvatar local, out string failure)
        {
            failure = "";
            if (!item || !item.IsSpawned || item.Holder.Value == PhysicsItem.Nobody)
            {
                failure = "Contested item was removed or has no holder.";
                return false;
            }
            if (item.OwnerClientId != item.Holder.Value)
            {
                failure = "Ownership does not match the winning holder.";
                return false;
            }
            int heldCount = 0;
            int players = 0;
            foreach (var player in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                if (!player.IsSpawned)
                    continue;
                players++;
                if (player.HeldItem.Value == item.NetworkObjectId)
                {
                    heldCount++;
                    if (player.OwnerClientId != item.Holder.Value)
                    {
                        failure = "A losing player's hands claim the contested item.";
                        return false;
                    }
                }
            }
            if (players != 2 || heldCount != 1)
            {
                failure = $"Expected two players and exactly one holder; players={players}, holders={heldCount}.";
                return false;
            }
            if (local.OwnerClientId != item.Holder.Value && local.HeldItem.Value != PhysicsItem.Nobody)
            {
                failure = "The losing local player still believes its hands are occupied.";
                return false;
            }
            if (root.Network.IsServer && (!item.Body.isKinematic || PhysicsItem.Count != 1))
            {
                failure = "Host's held-body mode or bounded item count is incorrect.";
                return false;
            }
            return true;
        }

        private void Fail(string reason)
        {
            if (finished)
                return;
            finished = true;
            StopAllCoroutines();
            Debug.LogError("RACE_FAIL " + reason);
            root.Session.Leave();
            Application.Quit(9);
        }
    }
}
