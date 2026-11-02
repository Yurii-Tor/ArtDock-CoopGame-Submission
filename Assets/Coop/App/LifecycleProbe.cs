using System;
using System.Collections;
using UnityEngine;

namespace Coop.App
{
    // Opt-in integration probe. Normal interactive sessions never attach it.
    public sealed class LifecycleProbe : MonoBehaviour
    {
        private CompositionRoot root;
        private float deadline;
        private bool failed;

        public void Initialize(CompositionRoot composition)
        {
            root = composition;
            deadline = Time.realtimeSinceStartup + 30;
            StartCoroutine(root.Options.Get("lifecycle") == "host" ? Host() : Client());
        }

        private IEnumerator Await(Func<bool> condition, string stage)
        {
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (condition())
                yield break;
            Fail(stage + ": " + root.Session.Status);
        }

        private IEnumerator Host()
        {
            yield return Await(() => root.Network.IsHost && root.Session.State && root.Session.State.Players.Count == 2, "host waiting for peer");
            if (failed)
                yield break;
            root.Session.StartGame();
            yield return Await(() => root.Session.LocalCanPlay && PeerReady(out _), "initial scene barrier");
            if (failed)
                yield break;
            PeerReady(out ulong firstPeer);
            yield return new WaitForSecondsRealtime(3);
            root.Session.Kick(firstPeer);
            Debug.Log("LIFECYCLE_HOST_KICK_SENT client=" + firstPeer);
            yield return Await(() => PeerReady(out ulong peer) && peer != firstPeer, "host waiting for rejoined peer");
            if (failed)
                yield break;
            Debug.Log("LIFECYCLE_HOST_REJOIN_OBSERVED");
            yield return new WaitForSecondsRealtime(10);
            root.Session.Leave();
            yield return new WaitForSecondsRealtime(1);
            if (root.Session.Status != "You left the session.")
            {
                Fail("intentional leave reason was overwritten: " + root.Session.Status);
                yield break;
            }
            Debug.Log("HOST_LOCAL_LEAVE_PASS");
            Application.Quit();
        }

        private IEnumerator Client()
        {
            yield return Await(() => root.Session.LocalCanPlay, "client first game");
            if (failed)
                yield break;
            yield return Await(() => !root.Network.IsListening && !root.Session.State, "client kicked back to menu");
            if (failed)
                yield break;
            if (root.Session.Status.IndexOf("kicked", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Fail("missing kick reason: " + root.Session.Status);
                yield break;
            }
            Debug.Log("KICK_PASS " + root.Session.Status);
            yield return new WaitForSecondsRealtime(1);
            root.Session.Join(root.Options.Get("address", "127.0.0.1"),
                (ushort)root.Options.Number("port", 7777), root.Options.Get("name", "Lifecycle client"));
            yield return Await(() => root.Session.LocalCanPlay, "same profile rejoin to running game");
            if (failed)
                yield break;
            Debug.Log("REJOIN_PASS client=" + root.Network.LocalClientId);
            yield return Await(() => !root.Network.IsListening && !root.Session.State, "host exit returns client to menu");
            if (failed)
                yield break;
            if (root.Session.Status != "Connection lost or host left the session.")
            {
                Fail("wrong host loss reason: " + root.Session.Status);
                yield break;
            }
            Debug.Log("HOST_LOSS_PASS " + root.Session.Status);
            Application.Quit();
        }

        private bool PeerReady(out ulong id)
        {
            if (root.Session.State)
                foreach (var player in root.Session.State.Players)
                    if (player.ClientId != root.Network.LocalClientId && root.Session.CanPlay(player.ClientId))
                    {
                        id = player.ClientId;
                        return true;
                    }
            id = 0;
            return false;
        }

        private void Fail(string message)
        {
            failed = true;
            Debug.LogError("LIFECYCLE_FAIL " + message);
            Application.Quit(9);
        }
    }
}
