using System.Collections;
using Coop.Gameplay;
using Coop.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

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
            if (root.Options.Has("hold-loading"))
            {
                root.Network.SceneManager.OnSceneEvent += HoldSceneActivation;
                while (Time.realtimeSinceStartup < deadline)
                    yield return null;
                root.Network.SceneManager.OnSceneEvent -= HoldSceneActivation;
                Debug.LogError("SMOKE_FAIL loading client was not terminated by harness");
                Application.Quit(12);
                yield break;
            }
            if (root.Network.IsHost)
            {
                while (root.Session.State.Players.Count < root.Options.Number("wait-players", 2) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return new WaitForSecondsRealtime(2);
                root.Session.StartGame();
                if (root.Options.Has("expect-loading-disconnect"))
                {
                    while (Time.realtimeSinceStartup < deadline && !TwoReadyWithOnePending())
                        yield return null;
                    if (!TwoReadyWithOnePending())
                    {
                        Debug.LogError("SMOKE_FAIL initial loading barrier did not wait for the held client");
                        Application.Quit(13);
                        yield break;
                    }
                    Debug.Log("SMOKE_LOADING_BARRIER_WAITING players=3 ready=2 playing=false");
                }
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
            if (root.Options.Has("expect-loading-disconnect"))
            {
                if (root.Session.State.Players.Count != 2)
                {
                    Debug.LogError("SMOKE_FAIL loading disconnect did not remove the departed player");
                    Application.Quit(14);
                    yield break;
                }
                Debug.Log("SMOKE_LOADING_DISCONNECT_PASS players=2");
                yield return new WaitForSecondsRealtime(15);
                yield break;
            }
            if (root.Options.Has("expect-host-crash"))
            {
                yield return VerifyHostCrash();
                yield break;
            }
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

        // Hold only this opt-in client's real scene activation, so the external harness can
        // terminate it while the host is demonstrably waiting at the initial load barrier.
        void HoldSceneActivation(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType != SceneEventType.Load || sceneEvent.SceneName != "Arena" ||
                sceneEvent.AsyncOperation == null)
                return;
            sceneEvent.AsyncOperation.allowSceneActivation = false;
            Debug.Log("SMOKE_LOADING_HELD scene=Arena activation=false");
        }

        bool TwoReadyWithOnePending()
        {
            var state = root.Session.State;
            if (!state || state.Phase.Value != SessionPhase.Loading || state.Players.Count != 3 || root.Session.LocalCanPlay)
                return false;
            int ready = 0;
            foreach (var player in state.Players)
                if (player.Ready)
                    ready++;
            return ready == 2;
        }

        IEnumerator VerifyHostCrash()
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while ((root.Network.IsListening || root.Network.ShutdownInProgress || root.Session.IsBusy ||
                root.Session.State != null || root.Session.LocalCanPlay) && Time.realtimeSinceStartup < deadline)
                yield return null;
            const string reason = "Connection lost or host left the session.";
            if (root.Network.IsListening || root.Network.ShutdownInProgress || root.Session.IsBusy ||
                root.Session.State != null || root.Session.LocalCanPlay || root.Session.Status != reason)
            {
                Debug.LogError("SMOKE_FAIL host crash model cleanup: " + root.Session.Status);
                Application.Quit(15);
                yield break;
            }
            Debug.Log("SMOKE_HOST_CRASH_MODEL_PASS " + root.Session.Status);
            // Wait through several normal view refreshes before checking the actual UI objects.
            yield return new WaitForSecondsRealtime(2);
            Button hostButton = null;
            Button joinButton = null;
            var view = root.GetComponent<SessionView>();
            foreach (var button in view.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "Host session") hostButton = button;
                if (button.name == "Join session") joinButton = button;
            }
            bool reasonActive = false;
            foreach (var text in view.GetComponentsInChildren<Text>(true))
                reasonActive |= text.isActiveAndEnabled && text.text.Contains(reason);
            if (root.Session.Status != reason || !reasonActive || !hostButton || !joinButton ||
                !hostButton.isActiveAndEnabled || !joinButton.isActiveAndEnabled ||
                !hostButton.interactable || !joinButton.interactable)
            {
                Debug.LogError("SMOKE_FAIL host crash menu state or reason");
                Application.Quit(16);
                yield break;
            }
            Debug.Log("SMOKE_HOST_CRASH_UI_STATE_PASS activeMenu=true hostEnabled=true joinEnabled=true reason=" + reason);
            // Exercising the real menu button verifies a new connection can be started in this process.
            hostButton.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 15;
            while ((!root.Network.IsHost || !root.Session.State || root.Session.State.Players.Count != 1) &&
                Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!root.Network.IsHost || !root.Session.State || root.Session.State.Players.Count != 1)
            {
                Debug.LogError("SMOKE_FAIL host crash menu could not start a fresh host: " + root.Session.Status);
                Application.Quit(17);
                yield break;
            }
            Debug.Log("SMOKE_HOST_CRASH_REHOST_PASS players=1");
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
