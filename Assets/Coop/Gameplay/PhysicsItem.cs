using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Coop.Gameplay
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public sealed class PhysicsItem : NetworkBehaviour
    {
        public const ulong Nobody = ulong.MaxValue;
        public readonly NetworkVariable<ulong> Holder = new(Nobody);
        private static readonly HashSet<PhysicsItem> Active = new();
        private Rigidbody body;

        public static int Count => Active.Count;
        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            gameObject.layer = 9;
            var networkBody = GetComponent<NetworkRigidbody>();
            if (networkBody)
            {
                // A held body must stay kinematic even on the authority. Keep one
                // writer for this state across NGO ownership callbacks.
                networkBody.AutoUpdateKinematicState = false;
                networkBody.AutoSetKinematicOnDespawn = true;
            }
        }

        public override void OnNetworkSpawn()
        {
            NetworkObject.DontDestroyWithOwner = true;
            Active.Add(this);
            Holder.OnValueChanged += OnHolderChanged;
            ApplyPhysicsMode();
            if (IsServer) Debug.Log($"[World] Spawn item={NetworkObjectId} owner={OwnerClientId} holder={Holder.Value}");
        }

        public override void OnNetworkDespawn()
        {
            Active.Remove(this);
            Holder.OnValueChanged -= OnHolderChanged;
        }

        private void OnHolderChanged(ulong previous, ulong current) => ApplyPhysicsMode();

        private void ApplyPhysicsMode()
        {
            // Ownership grants interaction identity, never simulation authority.
            // NetworkRigidbody reads the SERVER-authoritative NetworkTransform too.
            body.isKinematic = !IsServer || Holder.Value != Nobody;
            body.useGravity = Holder.Value == Nobody;
            body.interpolation = IsServer ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            body.collisionDetectionMode = body.isKinematic
                ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
        }

        public bool TryGrab(PlayerAvatar player)
        {
            if (!IsServer || Holder.Value != Nobody || player.HeldItem.Value != Nobody) return false;
            // All RPCs execute serially on the host. Commit both facts before returning.
            Holder.Value = player.OwnerClientId;
            player.HeldItem.Value = NetworkObjectId;
            NetworkObject.ChangeOwnership(player.OwnerClientId);
            ApplyPhysicsMode();
            Debug.Log($"[World] Grab item={NetworkObjectId} holder={Holder.Value} owner={OwnerClientId} serverKinematic={body.isKinematic}");
            return true;
        }

        public void Release(PlayerAvatar player, bool throwItem)
        {
            if (!IsServer || Holder.Value != player.OwnerClientId || player.HeldItem.Value != NetworkObjectId) return;
            player.HeldItem.Value = Nobody;
            Holder.Value = Nobody;
            NetworkObject.RemoveOwnership();
            ApplyPhysicsMode();
            body.linearVelocity = throwItem ? player.ViewDirection * 9f + Vector3.up * 1.5f : Vector3.zero;
            body.angularVelocity = throwItem ? Vector3.right * 3f : Vector3.zero;
            body.WakeUp();
            Debug.Log($"[World] {(throwItem ? "Throw" : "Release")} item={NetworkObjectId} owner={OwnerClientId} holder={Holder.Value} velocity={body.linearVelocity}");
        }

        public static void DropForPlayer(ulong clientId)
        {
            foreach (var item in Active)
            {
                if (!item || !item.IsServer || item.Holder.Value != clientId) continue;
                item.Holder.Value = Nobody;
                item.NetworkObject.RemoveOwnership();
                item.ApplyPhysicsMode();
                item.body.linearVelocity = Vector3.zero;
                item.body.angularVelocity = Vector3.zero;
                item.body.WakeUp();
                Debug.Log($"[World] Disconnect drop item={item.NetworkObjectId} client={clientId} owner={item.OwnerClientId}");
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (Holder.Value != Nobody)
            {
                if (!NetworkManager.ConnectedClients.TryGetValue(Holder.Value, out var client)
                    || !client.PlayerObject || !client.PlayerObject.TryGetComponent<PlayerAvatar>(out var player))
                {
                    DropForPlayer(Holder.Value);
                    return;
                }
                var eye = player.transform.position + Vector3.up * 1.45f;
                var distance = 1.6f;
                // Prevent a carried object being pushed into level walls.
                if (Physics.SphereCast(eye, 0.38f, player.ViewDirection, out var hit, distance,
                        1 << 0, QueryTriggerInteraction.Ignore)) distance = Mathf.Max(0.45f, hit.distance - 0.05f);
                body.MovePosition(eye + player.ViewDirection * distance);
                body.MoveRotation(player.transform.rotation);
            }
            else if (transform.position.y < -15f)
            {
                // A bounded cleanup policy keeps the demo running indefinitely.
                NetworkObject.Despawn();
            }
        }
    }
}
