using System.Collections;
using Coop.Gameplay;
using Coop.Session;
using UnityEngine;

namespace Coop.App
{
    // Opt-in build smoke harness; never runs in normal interactive sessions.
    public sealed class SmokeDriver : MonoBehaviour
    {
        CompositionRoot root;
        public void Initialize(CompositionRoot composition)
        {
            root = composition;
            StartCoroutine(Run());
        }
        IEnumerator Run()
        {
            float deadline = Time.realtimeSinceStartup + 45;
            if (root.Options.Has("expect-rejection"))
            {
                string expected = root.Options.Get("expect-rejection");
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (!root.Session.IsBusy && root.Session.Status.IndexOf(expected, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log("SMOKE_REJECTION_PASS " + root.Session.Status);
                        Application.Quit();
                        yield break;
                    }
                    if (root.Network.IsConnectedClient)
                    {
                        Debug.LogError("SMOKE_FAIL unexpectedly approved");
                        Application.Quit(7);
                        yield break;
                    }
                    yield return null;
                }
                Debug.LogError("SMOKE_FAIL missing rejection: " + root.Session.Status);
                Application.Quit(8);
                yield break;
            }
            while (!root.Network.IsConnectedClient && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!root.Network.IsConnectedClient)
            {
                Debug.LogError("SMOKE_FAIL connect " + root.Session.Status);
                Application.Quit(2);
                yield break;
            }
            Debug.Log("SMOKE_CONNECTED " + root.Network.LocalClientId);
            if (root.Network.IsHost)
            {
                while (root.Session.State.Players.Count < root.Options.Number("wait-players", 2) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return new WaitForSecondsRealtime(2);
                root.Session.StartGame();
            }
            while (!root.Session.LocalCanPlay && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!root.Session.LocalCanPlay)
            {
                Debug.LogError("SMOKE_FAIL loading " + root.Session.Status);
                Application.Quit(3);
                yield break;
            }
            Debug.Log("SMOKE_PLAYING players=" + root.Session.State.Players.Count);
            foreach (var existing in FindObjectsByType<PhysicsItem>(FindObjectsSortMode.None))
                Debug.Log($"SMOKE_SNAPSHOT item={existing.NetworkObjectId} holder={existing.Holder.Value} owner={existing.OwnerClientId}");
            if (root.Options.Has("expect-held"))
            {
                bool held = false;
                foreach (var existing in FindObjectsByType<PhysicsItem>(FindObjectsSortMode.None))
                    held |= existing.Holder.Value != PhysicsItem.Nobody;
                if (!held)
                {
                    Debug.LogError("SMOKE_FAIL late join held snapshot missing");
                    Application.Quit(9);
                    yield break;
                }
                Debug.Log("SMOKE_LATEJOIN_HELD_PASS");
            }
            var avatar = PlayerAvatar.Local;
            avatar.BotControlled = true;
            yield return new WaitForSecondsRealtime(1);
            avatar.RequestColor();
            avatar.RequestSpawn();
            yield return new WaitForSecondsRealtime(2);
            PhysicsItem nearest = null;
            float best = 4;
            foreach (var item in FindObjectsByType<PhysicsItem>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(avatar.transform.position, item.transform.position);
                Debug.Log($"SMOKE_GRAB_CANDIDATE item={item.NetworkObjectId} owner={item.OwnerClientId} holder={item.Holder.Value} position={item.transform.position} distance={distance:F3}");
                if (distance < best)
                {
                    best = distance;
                    nearest = item;
                }
            }
            if (!nearest)
            {
                Debug.LogError("SMOKE_FAIL no item");
                Application.Quit(4);
                yield break;
            }
            Debug.Log($"SMOKE_GRAB_SELECTED player={avatar.NetworkObjectId} client={avatar.OwnerClientId} playerPosition={avatar.transform.position} item={nearest.NetworkObjectId} itemPosition={nearest.transform.position} holder={nearest.Holder.Value} isOwner={avatar.IsOwner} ready={root.Session.LocalCanPlay}");
            avatar.RequestGrab(nearest.NetworkObjectId);
            float actionDeadline = Time.realtimeSinceStartup + 8;
            while (!avatar.HasItem && Time.realtimeSinceStartup < actionDeadline)
                yield return null;
            if (!avatar.HasItem)
            {
                Debug.LogError($"SMOKE_FAIL grab player={avatar.NetworkObjectId} spawned={avatar.IsSpawned} owner={avatar.OwnerClientId} position={avatar.transform.position} item={nearest.NetworkObjectId} itemHolder={nearest.Holder.Value} itemOwner={nearest.OwnerClientId} ready={root.Session.LocalCanPlay}");
                Application.Quit(5);
                yield break;
            }
            Debug.Log("SMOKE_HELD item=" + avatar.HeldItem.Value + " owner=" + nearest.OwnerClientId);
            yield return new WaitForSecondsRealtime(.5f);
            if (root.Options.Has("hold-exit"))
            {
                Debug.Log("SMOKE_HOLD_WAIT");
                yield return new WaitForSecondsRealtime(root.Options.Number("hold-seconds", 8));
                Debug.Log("SMOKE_HOLD_EXIT");
                Application.Quit();
                yield break;
            }
            avatar.RequestThrow();
            actionDeadline = Time.realtimeSinceStartup + 8;
            while ((avatar.HasItem || nearest.Holder.Value != PhysicsItem.Nobody) && Time.realtimeSinceStartup < actionDeadline)
                yield return null;
            yield return new WaitForSecondsRealtime(2);
            if (avatar.HasItem || nearest.Holder.Value != PhysicsItem.Nobody)
            {
                Debug.LogError("SMOKE_FAIL release");
                Application.Quit(6);
                yield break;
            }
            Debug.Log("SMOKE_THROWN item=" + nearest.NetworkObjectId + " owner=" + nearest.OwnerClientId);
            if (root.Options.Has("capture"))
            {
                avatar.transform.rotation = Quaternion.Euler(0, root.Network.IsHost ? 90 : -90, 0);
                avatar.RequestColor();
                StartCoroutine(DemoWalk(avatar));
            }
            float end = Time.realtimeSinceStartup + root.Options.Number("stay-seconds", 12);
            while (Time.realtimeSinceStartup < end)
            {
                Debug.Log("SMOKE_STATE players=" + (root.Session.State ? root.Session.State.Players.Count : 0) + " items=" + PhysicsItem.Count + " connected=" + root.Network.IsConnectedClient);
                if (root.Options.Has("expect-host-loss") && !root.Network.IsConnectedClient)
                {
                    if (root.Session.IsBusy || root.Session.State != null)
                    {
                        Debug.LogError("SMOKE_FAIL host loss cleanup");
                        Application.Quit(10);
                        yield break;
                    }
                    Debug.Log("SMOKE_HOST_LOSS_PASS " + root.Session.Status);
                    Application.Quit();
                    yield break;
                }
                yield return new WaitForSecondsRealtime(2);
            }
            if (root.Options.Has("expect-host-loss"))
            {
                Debug.LogError("SMOKE_FAIL host stayed connected");
                Application.Quit(11);
                yield break;
            }
            Debug.Log("SMOKE_PASS");
            root.Session.Leave();
            yield return new WaitForSecondsRealtime(1);
            Application.Quit();
        }

        IEnumerator DemoWalk(PlayerAvatar avatar)
        {
            // Exercise the same owner-authoritative CharacterController used by keyboard movement.
            float end = Time.realtimeSinceStartup + 2;
            var controller = avatar.GetComponent<CharacterController>();
            while (avatar && avatar.IsSpawned && Time.realtimeSinceStartup < end)
            {
                controller.Move((Vector3.forward * .5f + Vector3.down * 2) * Time.deltaTime);
                yield return null;
            }
        }
    }
}
