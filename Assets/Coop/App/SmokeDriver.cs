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
        public void Initialize(CompositionRoot composition) {root=composition;StartCoroutine(Run());}
        IEnumerator Run()
        {
            float deadline=Time.realtimeSinceStartup+45;
            while(!root.Network.IsConnectedClient && Time.realtimeSinceStartup<deadline)yield return null;
            if(!root.Network.IsConnectedClient){Debug.LogError("SMOKE_FAIL connect "+root.Session.Status);Application.Quit(2);yield break;}
            Debug.Log("SMOKE_CONNECTED "+root.Network.LocalClientId);
            if(root.Network.IsHost)
            {
                while(root.Session.State.Players.Count<root.Options.Number("wait-players",2) && Time.realtimeSinceStartup<deadline)yield return null;
                yield return new WaitForSecondsRealtime(2);root.Session.StartGame();
            }
            while(!root.Session.LocalCanPlay && Time.realtimeSinceStartup<deadline)yield return null;
            if(!root.Session.LocalCanPlay){Debug.LogError("SMOKE_FAIL loading "+root.Session.Status);Application.Quit(3);yield break;}
            Debug.Log("SMOKE_PLAYING players="+root.Session.State.Players.Count);
            var avatar=PlayerAvatar.Local;avatar.BotControlled=true;
            yield return new WaitForSecondsRealtime(1);
            avatar.RequestColor();avatar.RequestSpawn();
            yield return new WaitForSecondsRealtime(2);
            PhysicsItem nearest=null;float best=4;
            foreach(var item in FindObjectsByType<PhysicsItem>(FindObjectsSortMode.None))
            {
                float distance=Vector3.Distance(avatar.transform.position,item.transform.position);
                if(distance<best){best=distance;nearest=item;}
            }
            if(!nearest){Debug.LogError("SMOKE_FAIL no item");Application.Quit(4);yield break;}
            avatar.RequestGrab(nearest.NetworkObjectId);
            yield return new WaitForSecondsRealtime(1);
            if(!avatar.HasItem){Debug.LogError("SMOKE_FAIL grab");Application.Quit(5);yield break;}
            Debug.Log("SMOKE_HELD item="+avatar.HeldItem.Value+" owner="+nearest.OwnerClientId);
            if(root.Options.Has("hold-exit")) {Debug.Log("SMOKE_HOLD_EXIT");Application.Quit();yield break;}
            avatar.RequestThrow();
            yield return new WaitForSecondsRealtime(2);
            if(avatar.HasItem || nearest.Holder.Value!=PhysicsItem.Nobody){Debug.LogError("SMOKE_FAIL release");Application.Quit(6);yield break;}
            Debug.Log("SMOKE_THROWN item="+nearest.NetworkObjectId+" owner="+nearest.OwnerClientId);
            float end=Time.realtimeSinceStartup+root.Options.Number("stay-seconds",12);
            while(Time.realtimeSinceStartup<end)
            {
                Debug.Log("SMOKE_STATE players="+(root.Session.State?root.Session.State.Players.Count:0)+" items="+PhysicsItem.Count+" connected="+root.Network.IsConnectedClient);
                yield return new WaitForSecondsRealtime(2);
            }
            Debug.Log("SMOKE_PASS");root.Session.Leave();yield return new WaitForSecondsRealtime(1);Application.Quit();
        }
    }
}
