using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Coop.Gameplay
{
    [RequireComponent(typeof(CharacterController), typeof(NetworkObject))]
    public sealed class PlayerAvatar : NetworkBehaviour
    {
        // Composition notification, not a global service lookup. The root injects before use.
        public static event Action<PlayerAvatar> Spawned;
        public static PlayerAvatar Local
        {
            get; private set;
        }
        public readonly NetworkVariable<ulong> HeldItem = new(PhysicsItem.Nobody);
        public readonly NetworkVariable<int> ColorIndex = new(0);
        public readonly NetworkVariable<float> ViewPitch = new(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private static readonly Color[] Colors = {
            new(0.17f, 0.68f, 0.94f), new(0.98f, 0.42f, 0.25f),
            new(0.37f, 0.86f, 0.48f), new(0.8f, 0.4f, 0.98f)
        };
        private GameplayServices services;
        private CharacterController controller;
        private Camera view;
        private float pitch;
        private float verticalSpeed;
        private float nextLookSend;
        private double nextSpawn;
        private double nextAction;
        private double nextColor;
        private Renderer[] renderers;
        private MaterialPropertyBlock materialProperties;
        public bool BotControlled
        {
            get; set;
        }
        public Camera ViewCamera => view;
        public bool HasItem => IsSpawned && HeldItem.Value != PhysicsItem.Nobody;
        public bool HasValidWorldPose
        {
            get
            {
                var p = transform.position;
                var forward = transform.forward;
                return float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z)
                    && float.IsFinite(forward.x) && float.IsFinite(forward.y) && float.IsFinite(forward.z)
                    && Mathf.Abs(p.x) < 25 && Mathf.Abs(p.z) < 25 && p.y > -5 && p.y < 12;
            }
        }
        public Vector3 ViewDirection
        {
            get
            {
                var p = float.IsFinite(ViewPitch.Value) ? Mathf.Clamp(ViewPitch.Value, -75, 75) : 0;
                return Quaternion.AngleAxis(p, transform.right) * transform.forward;
            }
        }

        public void Configure(GameplayServices dependencies) => services = dependencies;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            renderers = GetComponentsInChildren<Renderer>();
            materialProperties = new MaterialPropertyBlock();
            foreach (var child in GetComponentsInChildren<Transform>())
                child.gameObject.layer = 8;
        }

        public override void OnNetworkSpawn()
        {
            Spawned?.Invoke(this);
            controller.enabled = IsOwner;
            ColorIndex.OnValueChanged += OnColorChanged;
            ApplyColor(ColorIndex.Value);
            if (!IsOwner)
                return;
            Local = this;
            var cameraObject = new GameObject("Local view", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = Vector3.up * 1.45f;
            view = cameraObject.GetComponent<Camera>();
            view.nearClipPlane = 0.05f;
            view.fieldOfView = 75f;
            foreach (var renderer in renderers)
                renderer.enabled = false;
        }

        public override void OnNetworkDespawn()
        {
            ColorIndex.OnValueChanged -= OnColorChanged;
            if (IsServer)
                PhysicsItem.DropForPlayer(OwnerClientId);
            if (Local == this)
            {
                Local = null;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (view)
                Destroy(view.gameObject);
        }

        private void OnColorChanged(int previous, int current) => ApplyColor(current);

        private void ApplyColor(int index)
        {
            var color = Colors[Mathf.Abs(index % Colors.Length)];
            materialProperties.SetColor("_Color", color);
            materialProperties.SetColor("_BaseColor", color);
            foreach (var renderer in renderers)
                renderer.SetPropertyBlock(materialProperties);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || services == null || !services.CanPlay(OwnerClientId) || BotControlled)
                return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi())
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }
            var look = mouse.delta.ReadValue() * 0.12f;
            transform.Rotate(0, look.x, 0);
            pitch = Mathf.Clamp(pitch - look.y, -75f, 75f);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (Time.unscaledTime >= nextLookSend)
            {
                ViewPitch.Value = pitch;
                nextLookSend = Time.unscaledTime + 0.1f;
            }
            var move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            if (controller.isGrounded && verticalSpeed < 0)
                verticalSpeed = -2;
            if (keyboard.spaceKey.wasPressedThisFrame && controller.isGrounded)
                verticalSpeed = 6;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            var planar = transform.TransformDirection(new Vector3(move.x, 0, move.y).normalized) * 5;
            controller.Move((planar + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (transform.position.y < -10f)
            {
                controller.enabled = false;
                transform.position = new Vector3(0, 2, -5);
                controller.enabled = true;
                verticalSpeed = 0;
            }
            if (keyboard.fKey.wasPressedThisFrame)
                RequestSpawn();
            if (keyboard.eKey.wasPressedThisFrame)
            {
                if (HasItem)
                    RequestRelease();
                else if (Physics.Raycast(view.transform.position, view.transform.forward, out var hit, 3.5f,
                             ~(1 << 8), QueryTriggerInteraction.Ignore))
                {
                    var item = hit.collider.GetComponentInParent<PhysicsItem>();
                    if (item)
                        RequestGrab(item.NetworkObjectId);
                }
            }
            if (mouse.rightButton.wasPressedThisFrame && HasItem)
                RequestThrow();
            if (keyboard.xKey.wasPressedThisFrame && HasItem)
                RequestDelete();
            if (keyboard.cKey.wasPressedThisFrame)
                RequestColor();
        }

        private static bool IsPointerOverUi() => UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        public void RequestSpawn()
        {
            if (IsOwner && IsSpawned)
                SpawnItemRpc();
        }
        public void RequestGrab(ulong itemId)
        {
            if (!IsOwner || !IsSpawned)
            {
                Debug.LogWarning($"[World] GrabRequestBlocked player={NetworkObjectId} item={itemId} owner={OwnerClientId} isOwner={IsOwner} spawned={IsSpawned}");
                return;
            }
            Debug.Log($"[World] GrabRequested player={NetworkObjectId} client={OwnerClientId} item={itemId} position={transform.position}");
            InteractRpc(0, itemId);
        }
        public void RequestRelease()
        {
            if (IsOwner && IsSpawned)
                InteractRpc(1, HeldItem.Value);
        }
        public void RequestThrow()
        {
            if (IsOwner && IsSpawned)
                InteractRpc(2, HeldItem.Value);
        }
        public void RequestDelete()
        {
            if (IsOwner && IsSpawned)
                InteractRpc(3, HeldItem.Value);
        }
        public void RequestColor()
        {
            if (IsOwner && IsSpawned)
                ChangeColorRpc();
        }

        private bool IsValidActor(ulong sender, string action)
        {
            bool ready = services != null && services.CanPlay(sender);
            bool poseValid = HasValidWorldPose;
            if (sender == OwnerClientId && ready && poseValid)
                return true;
            Debug.LogWarning($"[World] ActorRejected action={action} player={NetworkObjectId} sender={sender} owner={OwnerClientId} services={services != null} ready={ready} poseValid={poseValid} position={transform.position} forward={transform.forward} spawned={IsSpawned}");
            if (sender == OwnerClientId)
                RejectRpc(ready ? "Your player position is outside the valid world." : "Your player is not ready for gameplay.");
            return false;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SpawnItemRpc(RpcParams rpcParams = default)
        {
            if (!IsValidActor(rpcParams.Receive.SenderClientId, "spawn"))
                return;
            if (Time.unscaledTimeAsDouble < nextSpawn)
            {
                RejectRpc("Spawn cooldown: one second.");
                return;
            }
            nextSpawn = Time.unscaledTimeAsDouble + 1;
            if (PhysicsItem.Count >= 40)
            {
                RejectRpc("The world already has 40 items.");
                return;
            }
            var position = transform.position + Vector3.up * 1.6f + transform.forward * 2f;
            if (Physics.CheckSphere(position, 0.5f, ~(1 << 8), QueryTriggerInteraction.Ignore))
            {
                RejectRpc("Not enough room to spawn here.");
                return;
            }
            var instance = Instantiate(services.ItemPrefab, position, Quaternion.identity);
            instance.GetComponent<NetworkObject>().Spawn(false);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void InteractRpc(byte action, ulong itemId, RpcParams rpcParams = default)
        {
            Debug.Log($"[World] InteractionReceived player={NetworkObjectId} sender={rpcParams.Receive.SenderClientId} action={action} item={itemId} position={transform.position}");
            if (!IsValidActor(rpcParams.Receive.SenderClientId, "interaction") || action > 3)
                return;
            if (Time.unscaledTimeAsDouble < nextAction)
            {
                Debug.LogWarning($"[World] InteractionRateLimited sender={rpcParams.Receive.SenderClientId} action={action} item={itemId} remaining={nextAction - Time.unscaledTimeAsDouble:F3}");
                return;
            }
            nextAction = Time.unscaledTimeAsDouble + 0.1;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemId, out var networkObject)
                || !networkObject.TryGetComponent<PhysicsItem>(out var item))
            {
                RejectRpc("That item no longer exists.");
                return;
            }
            var eye = transform.position + Vector3.up * 1.45f;
            if (Vector3.Distance(eye, item.transform.position) > 3.5f)
            {
                RejectRpc("That item is too far away.");
                return;
            }
            if (action == 0)
            {
                if (Physics.Linecast(eye, item.transform.position, out var hit, ~(1 << 8), QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<PhysicsItem>() != item)
                {
                    RejectRpc("An obstacle blocks that item.");
                    return;
                }
                if (!item.TryGrab(this))
                    RejectRpc("The item is already held, or your hands are full.");
            }
            else
            {
                if (HeldItem.Value != itemId || item.Holder.Value != OwnerClientId)
                {
                    RejectRpc("You are not holding that item.");
                    return;
                }
                item.Release(this, action == 2);
                if (action == 2)
                    ConfirmThrowRpc();
                if (action == 3)
                    item.NetworkObject.Despawn();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ChangeColorRpc(RpcParams rpcParams = default)
        {
            if (!IsValidActor(rpcParams.Receive.SenderClientId, "color") || Time.unscaledTimeAsDouble < nextColor)
                return;
            nextColor = Time.unscaledTimeAsDouble + 0.25;
            ColorIndex.Value = (ColorIndex.Value + 1) % Colors.Length;
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void RejectRpc(string reason) => services?.Report(reason);

        // Only the thrower's local platform account records an accepted gameplay event.
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void ConfirmThrowRpc() => services?.ItemThrown(OwnerClientId);
    }
}
