using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Coop.App
{
    // Opt-in build regression checks that invoke the same controls as a player.
    public sealed class UiProbe : MonoBehaviour
    {
        CompositionRoot root;
        SessionView view;

        public void Initialize(CompositionRoot composition)
        {
            root = composition;
            view = root.GetComponent<SessionView>();
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(.6f);
            string mode = root.Options.Get("ui-probe");
            var port = view.GetComponentsInChildren<InputField>(true).FirstOrDefault(field => field.name == "UDP port");
            bool steam = root.Transport is Coop.Steam.SteamConnectionTransport;
            if (steam && port != null)
            {
                Fail("Steam must not show a UDP port input.");
                yield break;
            }

            if (mode == "steam-unavailable")
            {
                if (!steam || string.IsNullOrEmpty(root.StartupNotice) || root.Network.IsListening || root.Session.IsBusy ||
                    !VisibleText("Steam unavailable.") || !VisibleText("Start Steam") || !VisibleText("--transport utp"))
                {
                    Fail("The first menu must show the real Steam initialization failure and recovery actions.");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(1);
                if (!VisibleText(root.StartupNotice))
                {
                    Fail("Steam startup error disappeared on refresh.");
                    yield break;
                }
            }
            else if (mode == "utp-port")
            {
                if (steam || port == null)
                {
                    Fail("UTP must have a UDP port input.");
                    yield break;
                }
                foreach (string invalid in new[] { "0", "65536", "not-a-port", "-1", "" })
                {
                    port.text = invalid;
                    foreach (string action in new[] { "Host session", "Join session" })
                    {
                        Control(action).onClick.Invoke();
                        yield return new WaitForSecondsRealtime(.6f);
                        if (root.Network.IsListening || root.Session.IsBusy || !VisibleText("Enter a port from 1 to 65535."))
                        {
                            Fail("Invalid UTP port was accepted or its explanation disappeared: " + invalid);
                            yield break;
                        }
                    }
                }
                port.text = root.Options.Get("probe-port", "17977");
                Control("Host session").onClick.Invoke();
                yield return WaitForConnection();
                if (!root.Network.IsHost || !root.Network.IsConnectedClient || VisibleText("Enter a port"))
                {
                    Fail("The corrected UTP port did not start a usable host: " + root.Session.Status);
                    yield break;
                }
            }
            else if (mode == "steam-invalid-lobby")
            {
                if (!steam || !string.IsNullOrEmpty(root.StartupNotice))
                {
                    Fail("This case requires an available Steam client.");
                    yield break;
                }
                var address = view.GetComponentsInChildren<InputField>(true).Single(field => field.name == "IP address / Steam lobby ID");
                foreach (string invalid in new[] { "127.0.0.1", "0", "not-a-lobby" })
                {
                    address.text = invalid;
                    Control("Join session").onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.6f);
                    if (root.Network.IsListening || root.Session.IsBusy || !VisibleText("Enter the numeric Steam lobby ID."))
                    {
                        Fail("Invalid Steam lobby ID did not retain its explanation: " + invalid);
                        yield break;
                    }
                }
            }
            else if (mode == "steam-host")
            {
                if (!steam || !string.IsNullOrEmpty(root.StartupNotice))
                {
                    Fail("This case requires an available Steam client.");
                    yield break;
                }
                Control("Host session").onClick.Invoke();
                yield return WaitForConnection();
                if (!root.Network.IsHost || !root.Network.IsConnectedClient)
                {
                    Fail("Steam Host failed with an unused invalid --port: " + root.Session.Status);
                    yield break;
                }
            }
            else
            {
                Fail("Unknown --ui-probe mode: " + mode);
                yield break;
            }
            Debug.Log("UI_PROBE_PASS " + mode);
            yield return new WaitForSecondsRealtime(1);
            root.Session.Leave();
            Application.Quit();
        }

        IEnumerator WaitForConnection()
        {
            float deadline = Time.realtimeSinceStartup + 22;
            while (!root.Network.IsConnectedClient && root.Session.IsBusy && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(.6f);
        }

        Button Control(string label) => view.GetComponentsInChildren<Button>(true).Single(button => button.name == label);

        bool VisibleText(string value)
        {
            Canvas.ForceUpdateCanvases();
            return view.GetComponentsInChildren<Text>().Any(text => text.text.Contains(value) &&
                text.enabled && text.rectTransform.rect.height + 1 >= text.preferredHeight);
        }

        static void Fail(string reason)
        {
            Debug.LogError("UI_PROBE_FAIL " + reason);
            Application.Quit(24);
        }
    }
}
