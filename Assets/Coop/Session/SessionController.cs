using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Coop.Infrastructure;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Coop.Session
{
    public sealed class SessionController : MonoBehaviour
    {
        public NetworkManager Network
        {
            get; private set;
        }
        public SessionState State
        {
            get; private set;
        }
        public string Status { get; private set; } = "Host a session or join by address.";
        public bool IsBusy
        {
            get; private set;
        }
        public bool LocalCanPlay => Network && CanPlay(Network.LocalClientId);
        public string RequestedBuildVersion { get; set; } = ApprovalPolicy.BuildVersion;
        private int maxPlayers = ApprovalPolicy.MaxPlayers;
        public int MaxPlayers
        {
            get => maxPlayers;
            set => maxPlayers = Math.Max(2, Math.Min(ApprovalPolicy.MaxPlayers, value));
        }
        public event Action Changed;
        public event Action<ulong> PlayerDisconnected;

        private SessionState statePrefab;
        private IConnectionTransport transport;
        private IPlayerIdentity identity;
        private IGameStats stats;
        private readonly Dictionary<ulong, JoinPayload> reservations = new Dictionary<ulong, JoinPayload>();
        private readonly Dictionary<ulong, float> reservedAt = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> loadingSince = new Dictionary<ulong, float>();
        private readonly HashSet<ulong> confirmedClients = new HashSet<ulong>();
        private readonly LoadingBarrier loadingBarrier = new LoadingBarrier();
        private float attemptStarted;
        private float nextRefresh;
        private int operation;
        private bool leaving;
        private bool locallyStopped = true;
        private bool sceneEventsAttached;

        public void Initialize(NetworkManager manager, SessionState prefab, IConnectionTransport connectionTransport,
            IPlayerIdentity playerIdentity, IGameStats gameStats)
        {
            Network = manager;
            statePrefab = prefab;
            transport = connectionTransport;
            identity = playerIdentity;
            stats = gameStats;
            Network.NetworkConfig.ConnectionApproval = true;
            Network.NetworkConfig.ClientConnectionBufferTimeout = 15;
            Network.NetworkConfig.LoadSceneTimeOut = 45;
            Network.ConnectionApprovalCallback += Approve;
            Network.OnServerStarted += ServerStarted;
            Network.OnClientConnectedCallback += Connected;
            Network.OnClientDisconnectCallback += Disconnected;
            Network.OnTransportFailure += TransportFailed;
            SessionState.Spawned += BindState;
        }

        public bool CanPlay(ulong clientId) => Network && Network.IsListening && State && State.IsSpawned &&
            State.Phase.Value == SessionPhase.Playing && State.IsReady(clientId) && Network.ServerTime.Time >= State.PlayAt.Value;

        public void Host(string address, ushort port, string playerName) => _ = Connect(true, address, port, playerName);
        public void Join(string address, ushort port, string playerName) => _ = Connect(false, address, port, playerName);

        private async Task Connect(bool host, string address, ushort port, string playerName)
        {
            if (IsBusy || Network.IsListening || Network.ShutdownInProgress)
                return;
            int attempt = ++operation;
            locallyStopped = false;
            IsBusy = true;
            attemptStarted = Time.realtimeSinceStartup;
            SetStatus(host ? "Starting host..." : "Connecting...");
            try
            {
                await transport.PrepareAsync(host, address, port, MaxPlayers);
                if (attempt != operation)
                    return;
                string displayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? playerName : identity.DisplayName;
                displayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
                // Limit UTF-8 bytes too: FixedString64Bytes has 61 usable bytes.
                while (displayName.Length > 24 || Encoding.UTF8.GetByteCount(displayName) > 60)
                    displayName = displayName.Substring(0, displayName.Length - 1);
                var payload = new JoinPayload { version = RequestedBuildVersion, playerId = identity.PlayerId, name = displayName };
                // NGO always accepts its own host, so validate before asking it to start.
                if (host)
                {
                    string error = ApprovalPolicy.RejectReason(payload, 0, new HashSet<string>(), MaxPlayers);
                    if (error.Length > 0)
                        throw new InvalidOperationException(error);
                }
                Network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
                bool started = host ? Network.StartHost() : Network.StartClient();
                if (!started)
                    throw new InvalidOperationException("Transport could not start. Check address and port.");
                AttachSceneEvents();
            }
            catch (Exception exception)
            {
                if (attempt == operation)
                    Stop("Connection failed: " + exception.Message);
                Debug.LogWarning(exception);
            }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            JoinPayload payload = null;
            if (request.Payload != null && request.Payload.Length <= 512)
            {
                try
                {
                    payload = JsonUtility.FromJson<JoinPayload>(Encoding.UTF8.GetString(request.Payload));
                }
                catch (ArgumentException) { }
            }
            var ids = new HashSet<string>();
            foreach (JoinPayload reserved in reservations.Values)
                ids.Add(reserved.playerId);
            string reason = ApprovalPolicy.RejectReason(payload, reservations.Count, ids, MaxPlayers);
            if (reason.Length == 0 && !transport.ValidateIdentity(request.ClientNetworkId, payload.playerId))
                reason = "Invalid player identity.";
            response.Approved = reason.Length == 0;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = reason;
            if (!response.Approved)
                return;
            reservations[request.ClientNetworkId] = payload;
            reservedAt[request.ClientNetworkId] = Time.realtimeSinceStartup;
        }

        private void ServerStarted()
        {
            AttachSceneEvents();
            var state = Instantiate(statePrefab);
            state.PlayerLimit.Value = MaxPlayers;
            state.NetworkObject.Spawn(false);
            // The host connection callback can run before OnServerStarted.
            foreach (ulong clientId in Network.ConnectedClientsIds)
                AddPlayer(clientId);
        }

        private void BindState(SessionState state)
        {
            State = state;
            State.Players.OnListChanged += RosterChanged;
            State.Phase.OnValueChanged += PhaseChanged;
            SetStatus("Connected.");
        }

        private void Connected(ulong clientId)
        {
            confirmedClients.Add(clientId);
            if (Network.IsServer)
                AddPlayer(clientId);
            if (clientId == Network.LocalClientId)
            {
                IsBusy = false;
                SetStatus("Connected.");
                AttachSceneEvents();
            }
        }

        private void AddPlayer(ulong clientId)
        {
            if (!State || State.Find(clientId) >= 0 || !reservations.TryGetValue(clientId, out JoinPayload profile))
                return;
            State.Players.Add(new PlayerRecord
            {
                ClientId = clientId,
                PlayerId = new FixedString64Bytes(profile.playerId),
                Name = new FixedString64Bytes(profile.name),
                Ready = false,
                Rtt = 0
            });
            if (State.Phase.Value != SessionPhase.Lobby)
                loadingSince[clientId] = Time.realtimeSinceStartup;
            loadingBarrier.Add(clientId);
            UpdatePresence();
        }

        public void StartGame()
        {
            if (!Network.IsServer || !State || State.Phase.Value != SessionPhase.Lobby)
                return;
            State.Phase.Value = SessionPhase.Loading;
            var participants = new List<ulong>();
            foreach (PlayerRecord player in State.Players)
                participants.Add(player.ClientId);
            loadingBarrier.Begin(participants);
            for (int i = 0; i < State.Players.Count; i++)
            {
                PlayerRecord player = State.Players[i];
                player.Ready = false;
                State.Players[i] = player;
                loadingSince[player.ClientId] = Time.realtimeSinceStartup;
            }
            SceneEventProgressStatus result = Network.SceneManager.LoadScene("Arena", LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started)
            {
                State.Phase.Value = SessionPhase.Lobby;
                loadingBarrier.Reset();
                loadingSince.Clear();
                SetStatus("Scene load could not start: " + result);
            }
        }

        private void AttachSceneEvents()
        {
            if (sceneEventsAttached || Network.SceneManager == null)
                return;
            Network.SceneManager.OnSceneEvent += SceneChanged;
            sceneEventsAttached = true;
        }

        private void SceneChanged(SceneEvent sceneEvent)
        {
            if (!Network.IsServer || !State || State.Phase.Value == SessionPhase.Lobby)
                return;
            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.SceneName == "Arena")
                MarkReady(sceneEvent.ClientId);
            else if (sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete)
                MarkReady(sceneEvent.ClientId);
            else if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                foreach (ulong clientId in sceneEvent.ClientsThatTimedOut)
                    if (clientId != Network.LocalClientId)
                        Network.DisconnectClient(clientId, "Scene loading timed out.");
                TryOpenBarrier();
            }
        }

        private void MarkReady(ulong clientId)
        {
            AddPlayer(clientId);
            int index = State.Find(clientId);
            if (index < 0)
                return;
            PlayerRecord player = State.Players[index];
            player.Ready = true;
            State.Players[index] = player;
            loadingSince.Remove(clientId);
            loadingBarrier.MarkReady(clientId);
            if (Network.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject == null)
            {
                var avatar = Instantiate(Network.NetworkConfig.PlayerPrefab,
                    new Vector3((clientId % 4) * 3f - 4.5f, 0.1f, -5f), Quaternion.identity);
                avatar.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, false);
            }
            TryOpenBarrier();
        }

        private void TryOpenBarrier()
        {
            if (!State || State.Phase.Value != SessionPhase.Loading || !loadingBarrier.TryOpen())
                return;
            // All screens target a shared network timestamp, not local packet arrival.
            State.PlayAt.Value = Network.ServerTime.Time + 0.75;
            State.Phase.Value = SessionPhase.Playing;
            UpdatePresence();
        }

        public void Kick(ulong clientId)
        {
            if (Network.IsServer && clientId != Network.LocalClientId)
                Network.DisconnectClient(clientId, "You were kicked by the host.");
        }

        private void Disconnected(ulong clientId)
        {
            if (leaving || locallyStopped)
                return;
            if (Network.IsServer)
            {
                PlayerDisconnected?.Invoke(clientId);
                reservations.Remove(clientId);
                confirmedClients.Remove(clientId);
                reservedAt.Remove(clientId);
                loadingSince.Remove(clientId);
                loadingBarrier.Remove(clientId);
                if (State)
                {
                    int index = State.Find(clientId);
                    if (index >= 0)
                        State.Players.RemoveAt(index);
                }
                TryOpenBarrier();
                UpdatePresence();
            }
            else if (clientId == Network.LocalClientId)
            {
                string reason = Network.DisconnectReason;
                Stop(DisconnectMessages.ForUser(reason, IsBusy));
            }
        }

        private void TransportFailed()
        {
            if (!locallyStopped)
                Stop("Network transport failed. Check your connection.");
        }
        public void Leave() => Stop("You left the session.");
        public void Leave(string reason) => Stop(reason);

        private void Stop(string message)
        {
            if (leaving)
                return;
            leaving = true;
            locallyStopped = true;
            operation++;
            IsBusy = false;
            if (State)
            {
                State.Players.OnListChanged -= RosterChanged;
                State.Phase.OnValueChanged -= PhaseChanged;
            }
            if (sceneEventsAttached && Network.SceneManager != null)
                Network.SceneManager.OnSceneEvent -= SceneChanged;
            sceneEventsAttached = false;
            stats?.Flush();
            if (Network.IsListening)
                Network.Shutdown();
            transport?.Shutdown();
            State = null;
            reservations.Clear();
            confirmedClients.Clear();
            reservedAt.Clear();
            loadingSince.Clear();
            loadingBarrier.Reset();
            SetStatus(message);
            leaving = false;
        }

        private void Update()
        {
            if (Network == null)
                return;
            transport?.Tick();
            if (IsBusy && Time.realtimeSinceStartup - attemptStarted > 20)
            {
                Stop("Connection timed out. Check host address, port and firewall.");
                return;
            }
            if (!Network.IsServer || !State || Time.realtimeSinceStartup < nextRefresh)
                return;
            nextRefresh = Time.realtimeSinceStartup + 1f;
            var timedOut = new List<ulong>();
            foreach (var pending in reservedAt)
                if (!confirmedClients.Contains(pending.Key) && Time.realtimeSinceStartup - pending.Value > 20)
                    timedOut.Add(pending.Key);
            foreach (ulong clientId in timedOut)
            {
                if (Network.ConnectedClients.ContainsKey(clientId))
                    Network.DisconnectClient(clientId, "Connection synchronization timed out.");
                reservations.Remove(clientId);
                reservedAt.Remove(clientId);
            }
            timedOut.Clear();
            foreach (var pending in loadingSince)
                if (pending.Key != Network.LocalClientId && Time.realtimeSinceStartup - pending.Value > 45)
                    timedOut.Add(pending.Key);
            foreach (ulong clientId in timedOut)
                Network.DisconnectClient(clientId, "Scene loading timed out.");
            for (int i = 0; i < State.Players.Count; i++)
            {
                PlayerRecord player = State.Players[i];
                player.Rtt = player.ClientId == Network.LocalClientId ? 0 : (uint)Math.Min(transport.GetRtt(player.ClientId), uint.MaxValue);
                if (!player.Equals(State.Players[i]))
                    State.Players[i] = player;
            }
        }

        private void RosterChanged(NetworkListEvent<PlayerRecord> _)
        {
            UpdatePresence();
            Changed?.Invoke();
        }
        private void PhaseChanged(SessionPhase previous, SessionPhase current)
        {
            UpdatePresence();
            Changed?.Invoke();
        }
        private void SetStatus(string message)
        {
            Status = message;
            Debug.Log("[Session] " + message);
            Changed?.Invoke();
        }
        private void UpdatePresence() => transport?.SetSessionState(State && State.Phase.Value == SessionPhase.Playing, State ? State.Players.Count : 0, State ? State.PlayerLimit.Value : MaxPlayers);

        private void OnApplicationQuit() => stats?.Flush();

        private void OnDestroy()
        {
            SessionState.Spawned -= BindState;
            if (!Network)
                return;
            Network.ConnectionApprovalCallback -= Approve;
            Network.OnServerStarted -= ServerStarted;
            Network.OnClientConnectedCallback -= Connected;
            Network.OnClientDisconnectCallback -= Disconnected;
            Network.OnTransportFailure -= TransportFailed;
        }
    }
}
