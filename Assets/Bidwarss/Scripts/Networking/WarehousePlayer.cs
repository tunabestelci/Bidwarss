using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bidwarss
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WarehousePlayer : NetworkBehaviour
    {
        public Transform body;
        public static WarehousePlayer Local { get; private set; }
        public static readonly Dictionary<ulong, WarehousePlayer> Players = new Dictionary<ulong, WarehousePlayer>();
        Camera eye;
        CharacterController motor;
        Vector2 serverMove;
        float yaw, pitch, verticalSpeed, nextSend, lastInput, nextAction;
        InteractionTarget looked;
        public string CurrentHint { get; private set; }

        public override void OnNetworkSpawn()
        {
            Players[OwnerClientId] = this;
            motor = GetComponent<CharacterController>();
            motor.enabled = IsServer;
            if (!IsOwner) return;
            Local = this;
            yaw = transform.eulerAngles.y;
            body.gameObject.SetActive(false);
            var cameraObject = new GameObject("Local camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = Vector3.up * 1.55f;
            eye = cameraObject.GetComponent<Camera>();
            eye.nearClipPlane = .05f;
            eye.fieldOfView = 78;
            LockCursor(true);
        }

        public override void OnNetworkDespawn()
        {
            if (Players.TryGetValue(OwnerClientId, out var player) && player == this) Players.Remove(OwnerClientId);
            if (Local == this) { Local = null; LockCursor(false); }
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || Keyboard.current == null || Mouse.current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard.escapeKey.wasPressedThisFrame) LockCursor(Cursor.lockState != CursorLockMode.Locked);
            bool active = Cursor.lockState == CursorLockMode.Locked;
            Vector2 movement = Vector2.zero;
            if (active)
            {
                var delta = Mouse.current.delta.ReadValue();
                yaw = Mathf.Repeat(yaw + delta.x * .12f, 360);
                pitch = Mathf.Clamp(pitch - delta.y * .12f, -80, 80);
                movement = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            }
            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + .05f;
                MoveRpc(Vector2.ClampMagnitude(movement, 1), yaw);
            }
            looked = null;
            if (active && Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 3.5f,
                    ~0, QueryTriggerInteraction.Ignore))
                looked = hit.collider.GetComponent<InteractionTarget>();
            var world = WarehouseWorld.Instance;
            CurrentHint = world != null ? world.Hint(looked, OwnerClientId) : "";
            if (active && keyboard.eKey.wasPressedThisFrame && looked != null) InteractRpc(looked.kind, looked.id);
            if (active && keyboard.qKey.wasPressedThisFrame) DropRpc();
        }

        void LateUpdate()
        {
            if (IsOwner && eye != null) eye.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        void MoveRpc(Vector2 movement, float heading)
        {
            if (!Finite(movement.x) || !Finite(movement.y) || !Finite(heading)) return;
            serverMove = Vector2.ClampMagnitude(movement, 1);
            transform.rotation = Quaternion.Euler(0, Mathf.Repeat(heading, 360), 0);
            lastInput = Time.unscaledTime;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void InteractRpc(TargetKind kind, int id)
        {
            if (Time.unscaledTime < nextAction) return;
            nextAction = Time.unscaledTime + .12f;
            WarehouseWorld.Instance?.Act(this, kind, id);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void DropRpc()
        {
            if (Time.unscaledTime < nextAction) return;
            nextAction = Time.unscaledTime + .12f;
            WarehouseWorld.Instance?.Drop(this);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            Vector2 input = Time.unscaledTime - lastInput < .3f ? serverMove : Vector2.zero;
            Vector3 direction = transform.right * input.x + transform.forward * input.y;
            verticalSpeed = motor.isGrounded ? -2 : Mathf.Max(verticalSpeed - 20 * Time.fixedDeltaTime, -30);
            motor.Move((direction * 4.5f + Vector3.up * verticalSpeed) * Time.fixedDeltaTime);
        }
    }
}
