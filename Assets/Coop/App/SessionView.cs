using System;
using System.Text;
using Coop.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Coop.App
{
    // A small runtime uGUI view. All networking actions go through the injected session.
    public sealed class SessionView : MonoBehaviour
    {
        CompositionRoot root;
        GameObject panel;
        GameObject connectionPanel;
        InputField nameInput, addressInput, portInput;
        Text status, roster, hud, scoreboard, inviteHint;
        Button host, join, start, leave, invite, copyLobby;
        string inviteNotice = "";
        string inviteLobbyId = "";
        string connectionNotice = "";
        Transform kickRows;
        float refreshAt;
        bool shown = true;
        bool previouslyPlaying;
        Font font;
        public void Initialize(CompositionRoot composition)
        {
            root = composition;
            connectionNotice = root.StartupNotice;
            root.Session.Changed += SessionChanged;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("Session UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var events = new GameObject("UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform);
            panel = new GameObject("Session panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(440, -32);
            rect.anchoredPosition = new Vector2(16, -16);
            panel.GetComponent<Image>().color = new Color(.04f, .07f, .10f, .96f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 7;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            Label(panel.transform, "DOCK / CO-OP", 28, 40);
            Label(panel.transform, "A shared physics playground · 2–4 players", 15, 24);
            connectionPanel = new GameObject("Connection settings", typeof(RectTransform), typeof(VerticalLayoutGroup));
            connectionPanel.transform.SetParent(panel.transform, false);
            var cl = connectionPanel.GetComponent<VerticalLayoutGroup>();
            cl.spacing = 5;
            cl.childForceExpandHeight = false;
            cl.childControlHeight = true;
            cl.childForceExpandWidth = true;
            nameInput = Input(connectionPanel.transform, "Display name", root.Options.Get("name", "Player"));
            addressInput = Input(connectionPanel.transform, "IP address / Steam lobby ID", root.Options.Get("address", "127.0.0.1"));
            if (!(root.Transport is Coop.Steam.SteamConnectionTransport))
                portInput = Input(connectionPanel.transform, "UDP port", root.Options.Get("port", "7777"));
            host = Button(connectionPanel.transform, "Host session", () => Connect(true));
            join = Button(connectionPanel.transform, "Join session", () => Connect(false));
            if (root.Transport is Coop.Steam.SteamConnectionTransport steam)
            {
                var actions = new GameObject("Steam invitations", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                actions.transform.SetParent(panel.transform, false);
                var actionsLayout = actions.GetComponent<HorizontalLayoutGroup>();
                actionsLayout.spacing = 7;
                actionsLayout.childForceExpandWidth = true;
                actionsLayout.childForceExpandHeight = false;
                invite = Button(actions.transform, "Invite Steam friends", () =>
                    inviteNotice = steam.TryOpenInviteOverlay(out var error)
                        ? "Steam invite dialog requested. If it does not appear, use Copy lobby ID."
                        : error);
                copyLobby = Button(actions.transform, "Copy lobby ID", () =>
                {
                    if (string.IsNullOrEmpty(steam.LobbyId))
                        return;
                    GUIUtility.systemCopyBuffer = steam.LobbyId;
                    inviteNotice = "Lobby ID copied. Send it to a friend to paste into the Steam lobby ID field.";
                });
                invite.interactable = copyLobby.interactable = false;
                inviteHint = Label(panel.transform, "Host or join a session to invite friends.", 14, 58);
            }
            start = Button(panel.transform, "Start game", () => root.Session.StartGame());
            leave = Button(panel.transform, "Leave session", () => root.Session.Leave());
            status = Label(panel.transform, connectionNotice, 15, 54);
            roster = Label(panel.transform, "", 16, 112);
            kickRows = new GameObject("Host moderation", typeof(RectTransform), typeof(VerticalLayoutGroup)).transform;
            kickRows.SetParent(panel.transform, false);
            hud = Label(canvasGo.transform, "", 17, 0);
            var hr = hud.rectTransform;
            hr.anchorMin = new Vector2(0, 0);
            hr.anchorMax = new Vector2(1, 0);
            hr.pivot = new Vector2(.5f, 0);
            hr.sizeDelta = new Vector2(-32, 75);
            hr.anchoredPosition = new Vector2(0, 10);
            hud.alignment = TextAnchor.MiddleCenter;
            var cross = Label(canvasGo.transform, "+", 24, 0);
            cross.rectTransform.anchorMin = cross.rectTransform.anchorMax = new Vector2(.5f, .5f);
            cross.rectTransform.sizeDelta = new Vector2(30, 30);
            cross.alignment = TextAnchor.MiddleCenter;
            scoreboard = Label(canvasGo.transform, "", 17, 0);
            scoreboard.rectTransform.anchorMin = scoreboard.rectTransform.anchorMax = Vector2.one;
            scoreboard.rectTransform.pivot = Vector2.one;
            scoreboard.rectTransform.anchoredPosition = new Vector2(-20, -20);
            scoreboard.rectTransform.sizeDelta = new Vector2(340, 140);
            scoreboard.alignment = TextAnchor.UpperRight;
            if (root.Options.Has("host"))
                Invoke(nameof(AutoHost), 1);
            else if (root.Options.Has("join"))
                Invoke(nameof(AutoJoin), 1);
        }
        void AutoHost() => Connect(true);
        void AutoJoin() => Connect(false);
        void Connect(bool isHost)
        {
            // Steam uses lobby IDs; preserve the shared session contract with an ignored port.
            ushort port = 7777;
            if (portInput != null && (!ushort.TryParse(portInput.text, out port) || port == 0))
            {
                connectionNotice = "Enter a port from 1 to 65535.";
                status.text = connectionNotice;
                return;
            }
            connectionNotice = "";
            if (isHost)
                root.Session.Host(addressInput.text, port, nameInput.text);
            else
                root.Session.Join(addressInput.text, port, nameInput.text);
        }
        void Update()
        {
            if (root == null)
                return;
            if (Keyboard.current?.tabKey.wasPressedThisFrame == true || Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                shown = !shown;
            bool playing = root.Session.LocalCanPlay;
            if (playing && !previouslyPlaying)
                shown = false;
            previouslyPlaying = playing;
            panel.SetActive(!playing || shown);
            bool capture = playing && !shown;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
            if (Time.unscaledTime < refreshAt)
                return;
            refreshAt = Time.unscaledTime + .25f;
            var session = root.Session;
            var network = root.Network;
            connectionPanel.SetActive(!network.IsConnectedClient);
            host.interactable = join.interactable = !network.IsListening && !session.IsBusy;
            start.gameObject.SetActive(network.IsHost && session.State != null && !playing);
            leave.gameObject.SetActive(network.IsListening || session.IsBusy);
            status.text = string.IsNullOrEmpty(connectionNotice) ? session.Status : connectionNotice;
            if (session.State != null)
                status.text += "\n" + session.State.Phase.Value;
            if (root.Transport is Coop.Steam.SteamConnectionTransport steam && !string.IsNullOrEmpty(steam.LobbyId))
                status.text += "\nLobby: " + steam.LobbyId;
            // Initialization and connection errors can wrap beyond the normal status height.
            status.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(54, status.preferredHeight + 4);
            if (root.Transport is Coop.Steam.SteamConnectionTransport steamInvites)
            {
                var lobbyId = steamInvites.LobbyId;
                if (inviteLobbyId != lobbyId)
                {
                    inviteLobbyId = lobbyId;
                    inviteNotice = "";
                }
                bool canInvite = network.IsConnectedClient && !string.IsNullOrEmpty(lobbyId);
                invite.interactable = copyLobby.interactable = canInvite;
                inviteHint.text = !canInvite ? "Host or join a session to invite friends."
                    : !string.IsNullOrEmpty(inviteNotice) ? inviteNotice
                    : "Invite friends through Steam Overlay, or copy the lobby ID.";
            }
            var sb = new StringBuilder();
            if (session.State != null)
                foreach (var p in session.State.Players)
                    sb.AppendLine($"{p.Name}  ·  {p.Rtt} ms  ·  {(p.Ready ? "ready" : "loading / lobby")}");
            roster.text = sb.ToString();
            roster.gameObject.SetActive(session.State != null);
            scoreboard.text = playing ? "SESSION / " + session.State.Players.Count + " of " + session.State.PlayerLimit.Value + "\n" + sb : "";
            hud.text = playing ? "WASD move · Mouse look · Space jump · F spawn · E grab/drop · Right click throw · X delete · C colour\nTab / Esc: players & cursor    " + root.Notice : "Host a session or join a host. The host starts the level once everyone is in the lobby.";
            foreach (Transform child in kickRows)
                Destroy(child.gameObject);
            if (network.IsHost && session.State != null)
                foreach (var p in session.State.Players)
                    if (p.ClientId != network.LocalClientId)
                    {
                        var id = p.ClientId;
                        Button(kickRows, "Kick " + p.Name, () => session.Kick(id));
                    }
        }
        void SessionChanged() => connectionNotice = "";
        void OnDestroy()
        {
            if (root != null && root.Session != null)
                root.Session.Changed -= SessionChanged;
        }
        Text Label(Transform parent, string value, int size, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.text = value;
            t.color = new Color(.9f, .95f, 1);
            t.raycastTarget = false;
            t.supportRichText = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .9f);
            shadow.effectDistance = new Vector2(1, -1);
            if (height > 0)
                go.AddComponent<LayoutElement>().preferredHeight = height;
            return t;
        }
        Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 34;
            go.GetComponent<Image>().color = new Color(.13f, .30f, .36f);
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(action);
            var t = Label(go.transform, label, 17, 0);
            t.alignment = TextAnchor.MiddleCenter;
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.sizeDelta = Vector2.zero;
            return b;
        }
        InputField Input(Transform parent, string label, string value)
        {
            Label(parent, label, 13, 18);
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 32;
            go.GetComponent<Image>().color = new Color(.12f, .16f, .2f);
            var t = Label(go.transform, "", 16, 0);
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.sizeDelta = new Vector2(-14, 0);
            t.alignment = TextAnchor.MiddleLeft;
            var field = go.GetComponent<InputField>();
            field.textComponent = t;
            field.characterLimit = 64;
            field.text = value;
            return field;
        }
    }
}
