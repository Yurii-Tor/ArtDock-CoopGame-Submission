using System;
using Coop.Gameplay;
using Coop.Session;
using Coop.Infrastructure;
using Coop.Steam;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.App
{
    public sealed class CompositionRoot : MonoBehaviour
    {
        public NetworkManager Network;
        public SessionState SessionPrefab;
        public GameObject ItemPrefab;
        public SessionController Session
        {
            get; private set;
        }
        public LaunchOptions Options
        {
            get; private set;
        }
        public IConnectionTransport Transport
        {
            get; private set;
        }
        public string Notice { get; private set; } = "";
        GameplayServices gameplay;
        IGameStats stats;
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var background = new GameObject("Menu background").AddComponent<Camera>();
            background.transform.SetParent(transform);
            background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = new Color(.025f, .045f, .065f);
            background.cullingMask = 0;
            background.depth = -100;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Physics.IgnoreLayerCollision(8, 9, true);
            Physics.IgnoreLayerCollision(8, 8, true);
            Options = new LaunchOptions();
            if (Options.Has("net-debug"))
                Network.LogLevel = LogLevel.Developer;
            Session = gameObject.AddComponent<SessionController>();
            var utp = Network.GetComponent<UnityTransport>();
            Transport = new UtpConnectionTransport(Network, utp);
            IPlayerIdentity identity = new LocalPlayerIdentity(Options.Get("profile", "default"));
            if (Options.Get("transport", "utp") == "steam")
            {
                var steam = new SteamConnectionTransport(Network, new SteamSessionOptions { LobbyName = Options.Get("lobby-name", "Dock Co-op"), FriendsOnly = !Options.Has("private") });
                Transport = steam;
                identity = steam;
                if (!steam.TryInitialize(out var error))
                    Notice = error;
                steam.InviteJoinRequested += lobbyId => Session.Join(lobbyId, 7777, "");
                steam.SessionEnded += reason => Session.Leave(reason);
            }
            stats = new LoggingGameStats();
            Session.Initialize(Network, SessionPrefab, Transport, identity, stats);
            Session.MaxPlayers = Options.Number("max-players", 4);
            Session.RequestedBuildVersion = Options.Get("build-version", ApprovalPolicy.BuildVersion);
            gameplay = new GameplayServices(ItemPrefab, id => Session.CanPlay(id), message => { Notice = message; Debug.Log(message); },
                id => { if (id == Network.LocalClientId) stats.RecordThrow(); });
            PlayerAvatar.Spawned += InjectPlayer;
            Session.PlayerDisconnected += PhysicsItem.DropForPlayer;
            SceneManager.sceneLoaded += SceneLoaded;
            gameObject.AddComponent<SessionView>().Initialize(this);
            if (Options.Has("smoke"))
                gameObject.AddComponent<SmokeDriver>().Initialize(this);
            if (Options.Has("race"))
                gameObject.AddComponent<RaceProbe>().Initialize(this);
            if (Options.Has("lifecycle"))
                gameObject.AddComponent<LifecycleProbe>().Initialize(this);
            if (Options.Has("capture"))
                gameObject.AddComponent<FrameRecorder>().Begin(Options.Get("capture"));
        }
        void InjectPlayer(PlayerAvatar avatar) => avatar.Configure(gameplay);
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Arena")
                ArenaFactory.Create(scene);
        }
        void OnDestroy()
        {
            PlayerAvatar.Spawned -= InjectPlayer;
            if (Session != null)
                Session.PlayerDisconnected -= PhysicsItem.DropForPlayer;
            SceneManager.sceneLoaded -= SceneLoaded;
            (Transport as IDisposable)?.Dispose();
        }
    }
}
