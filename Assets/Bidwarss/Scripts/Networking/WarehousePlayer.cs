using System.Collections.Generic;
using Bidwarss.Domain;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bidwarss
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class WarehousePlayer : NetworkBehaviour
    {
        public Transform body;
        public WarehouseGripRig Grip { get; private set; }
        public float LookDistance { get; private set; }
        public static WarehousePlayer Local { get; private set; }
        public static readonly Dictionary<ulong,WarehousePlayer> Players=new Dictionary<ulong,WarehousePlayer>();
        public readonly NetworkVariable<FixedString64Bytes> PlayerName=new NetworkVariable<FixedString64Bytes>();
        Camera eye;
        CharacterController motor;
        Vector2 serverMove;
        float yaw,pitch,serverPitch,verticalSpeed,nextSend,lastInput,nextAction;
        int wantedCrate=-1;
        public int WantsCrate => wantedCrate;
        public bool InputFresh => Time.unscaledTime-lastInput < .3f;
        public InteractionTarget Looked { get; private set; }
        public string CurrentHint { get; private set; }
        public string Toast { get; private set; }
        public float ToastUntil { get; private set; }
        public bool ResultsVisible { get; private set; }
        bool seenCompleted;
        string currentRun;
        public override void OnNetworkSpawn()
        {
            Players[OwnerClientId]=this; motor=GetComponent<CharacterController>(); motor.enabled=IsServer;
            Grip=gameObject.AddComponent<WarehouseGripRig>();Grip.Initialize(this);
            if(IsServer)PlayerName.Value=SessionMenu.Instance!=null?SessionMenu.Instance.NameFor(OwnerClientId):"Oyuncu";
            // Replace the original capsule visually; the CharacterController remains the sole body collider.
            Material avatarMaterial=null;
            if(body!=null)
            {
                var renderer=body.GetComponentInChildren<Renderer>();
                if(renderer!=null)avatarMaterial=renderer.sharedMaterial;
                if(body!=null)body.gameObject.SetActive(false);
            }
            var avatar=new GameObject("Warehouse worker");avatar.transform.SetParent(transform,false);
            avatar.AddComponent<WarehouseAvatar>().Initialize(this,avatarMaterial);
            avatar.SetActive(!IsOwner);
            if(!IsOwner)return;
            Local=this; yaw=transform.eulerAngles.y; if(body!=null)body.gameObject.SetActive(false);
            var go=new GameObject("Local camera",typeof(Camera),typeof(AudioListener));
            go.transform.SetParent(transform,false); go.transform.localPosition=Vector3.up*1.55f;
            Grip.UseCamera(go.transform);
            go.AddComponent<WarehouseHands>().Initialize(this,avatarMaterial);
            eye=go.GetComponent<Camera>(); eye.nearClipPlane=.05f; eye.fieldOfView=78; LockCursor(true);
        }
        public override void OnNetworkDespawn()
        {
            if(Players.TryGetValue(OwnerClientId,out var p) && p==this)Players.Remove(OwnerClientId);
            if(Local==this){Local=null;LockCursor(false);}
            if(Grip!=null){Destroy(Grip.CarryAnchor.gameObject);Destroy(Grip);Grip=null;}
        }
        public void ServerResetForRound()
        {
            if(!IsServer || SessionMenu.Instance==null)return;
            int seat=SessionMenu.Instance.SeatFor(OwnerClientId);var position=SessionMenu.Instance.SpawnPosition(seat);
            motor.enabled=false;
            GetComponent<NetworkTransform>().Teleport(position,Quaternion.identity,transform.localScale);
            motor.enabled=true;verticalSpeed=0;lastInput=-1;ClearOpenIntent();ResetViewRpc();
        }
        [Rpc(SendTo.Owner,InvokePermission=RpcInvokePermission.Server)]
        void ResetViewRpc(){yaw=0;pitch=0;}
        public void ClearOpenIntent(){wantedCrate=-1;serverMove=Vector2.zero;}
        public static void LockCursor(bool locked)
        {Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;}
        public void CloseResults(){ResultsVisible=false;LockCursor(true);}
        void Update()
        {
            if(!IsSpawned || !IsOwner || eye==null)return;
            var world=WarehouseWorld.Instance;
            if(world!=null && currentRun!=world.RunId.Value.ToString())
            {currentRun=world.RunId.Value.ToString();seenCompleted=false;ResultsVisible=false;LockCursor(true);}
            if(world!=null && world.Completed.Value && !seenCompleted)
            {seenCompleted=true;ResultsVisible=true;LockCursor(false);RevealEffects.Celebrate();}
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(keyboard==null || mouse==null)return;
            if(keyboard.escapeKey.wasPressedThisFrame) {ResultsVisible=false;LockCursor(Cursor.lockState!=CursorLockMode.Locked);}
            if(keyboard.tabKey.wasPressedThisFrame && world!=null && world.Completed.Value)
            {ResultsVisible=!ResultsVisible;LockCursor(!ResultsVisible);}
            bool active=Cursor.lockState==CursorLockMode.Locked && Application.isFocused;
            Vector2 movement=Vector2.zero;
            if(active)
            {
                var delta=mouse.delta.ReadValue();yaw=Mathf.Repeat(yaw+delta.x*.12f,360);pitch=Mathf.Clamp(pitch-delta.y*.12f,-80,80);
                movement=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
            }
            eye.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            Looked=null;LookDistance=float.PositiveInfinity;
            if(active && Physics.Raycast(eye.transform.position,eye.transform.forward,out var hit,3.5f,~0,QueryTriggerInteraction.Ignore))
            { Looked=hit.collider.GetComponentInParent<InteractionTarget>();LookDistance=hit.distance; }
            CurrentHint=world!=null?world.Hint(Looked,OwnerClientId):"";
            int openTarget=active && keyboard.eKey.isPressed && Looked!=null && Looked.kind==TargetKind.Crate?Looked.id:-1;
            if(Time.unscaledTime>=nextSend)
            {
                nextSend=Time.unscaledTime+.05f;
                InputRpc(Vector2.ClampMagnitude(movement,1),yaw,pitch,openTarget);
            }
            if(active && keyboard.eKey.wasPressedThisFrame && Looked!=null && Looked.kind!=TargetKind.Crate)
                InteractRpc(Looked.kind,Looked.id,yaw,pitch);
            if(active && keyboard.qKey.wasPressedThisFrame)DropRpc();
        }
        void LateUpdate(){if(IsOwner && eye!=null)eye.transform.rotation=Quaternion.Euler(pitch,yaw,0);}
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner,Delivery=RpcDelivery.Unreliable)]
        void InputRpc(Vector2 movement,float heading,float vertical,int crate)
        {
            if(!Finite(movement.x)||!Finite(movement.y)||!Finite(heading)||!Finite(vertical))return;
            serverMove=Vector2.ClampMagnitude(movement,1);transform.rotation=Quaternion.Euler(0,Mathf.Repeat(heading,360),0);
            serverPitch=Mathf.Clamp(vertical,-80,80);wantedCrate=crate;lastInput=Time.unscaledTime;
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        void InteractRpc(TargetKind kind,int id,float heading,float vertical)
        {
            if(Time.unscaledTime<nextAction||!Finite(heading)||!Finite(vertical))return;
            nextAction=Time.unscaledTime+.10f;
            transform.rotation=Quaternion.Euler(0,Mathf.Repeat(heading,360),0);serverPitch=Mathf.Clamp(vertical,-80,80);
            WarehouseWorld.Instance?.Act(this,kind,id);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        void DropRpc()
        {if(Time.unscaledTime<nextAction)return;nextAction=Time.unscaledTime+.1f;WarehouseWorld.Instance?.Drop(this);}
        public void Feedback(string text){if(IsServer)FeedbackRpc(text);}
        [Rpc(SendTo.Owner,InvokePermission=RpcInvokePermission.Server)]
        void FeedbackRpc(string text){Toast=text;ToastUntil=Time.unscaledTime+2.5f;}
        public bool ServerLooksAt(TargetKind kind,int id)
        {return ServerLookHit(kind,id,3.5f,out _);}
        public bool ServerLookHit(TargetKind kind,int id,float reach,out RaycastHit contact)
        {
            contact=default;if(!IsServer)return false;
            Vector3 origin=transform.position+Vector3.up*1.55f;
            Vector3 direction=Quaternion.Euler(serverPitch,transform.eulerAngles.y,0)*Vector3.forward;
            Physics.SyncTransforms();
            var hits=Physics.RaycastAll(origin,direction,reach,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(transform))continue;
                var target=hit.collider.GetComponentInParent<InteractionTarget>();
                if(target!=null && target.kind==kind && target.id==id){contact=hit;return true;}
                return false;
            }
            return false;
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        void FixedUpdate()
        {
            if(!IsSpawned||!IsServer)return;
            Vector2 input=InputFresh?serverMove:Vector2.zero;
            if(WarehouseWorld.Instance!=null && WarehouseWorld.Instance.TryOpening(OwnerClientId,out _,out _))input=Vector2.zero;
            Vector3 direction=transform.right*input.x+transform.forward*input.y;
            verticalSpeed=motor.isGrounded?-2:Mathf.Max(verticalSpeed-20*Time.fixedDeltaTime,-30);
            motor.Move((direction*4.5f+Vector3.up*verticalSpeed)*Time.fixedDeltaTime);
            if(transform.position.y < -5){motor.enabled=false;transform.position=SessionMenu.Instance!=null?SessionMenu.Instance.SpawnPosition(SessionMenu.Instance.SeatFor(OwnerClientId)):new Vector3(0,.1f,-11);motor.enabled=true;}
        }
    }
}
