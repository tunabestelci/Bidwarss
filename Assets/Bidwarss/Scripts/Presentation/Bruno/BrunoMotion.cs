using System;
using UnityEngine;
using Bidwarss.Domain;

namespace Bidwarss
{
    /// <summary>Visual-only animation. Networking/authority stays with WarehousePlayer.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class BrunoMotion : MonoBehaviour
    {
        [Range(0, 4.5f)] public float previewSpeed;
        public bool previewCarrying;
        public bool lookAround = true;
        public OpeningTool previewTool;
        Animator animator;
        Transform head;
        Transform upperL,lowerL,handL,upperR,lowerR,handR;
        Quaternion restHandL,restHandR;
        SkinnedMeshRenderer lids;
        SkinnedMeshRenderer fingerMesh;
        int gripL=-1,gripR=-1;
        int blinkLeft = -1, blinkRight = -1;
        WarehousePlayer owner;
        Vector3 previous;
        float speed, time, nextBlink, blinkStart = -10, greetUntil;
        int state;
        System.Random random;
        static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
        static readonly int CarryIdle = Animator.StringToHash("Base Layer.CarryIdle");
        static readonly int CarryWalk = Animator.StringToHash("Base Layer.CarryWalk");
        static readonly int GreetState = Animator.StringToHash("Base Layer.Greet");
        static readonly int CutState=Animator.StringToHash("Base Layer.BoxCut");
        static readonly int PryState=Animator.StringToHash("Base Layer.Pry");

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="Head")head=t;
                if(t.name=="UpperArm.L")upperL=t;if(t.name=="Forearm.L")lowerL=t;if(t.name=="Hand.L")handL=t;
                if(t.name=="UpperArm.R")upperR=t;if(t.name=="Forearm.R")lowerR=t;if(t.name=="Hand.R")handR=t;
            }
            if(handL!=null)restHandL=Quaternion.Inverse(transform.rotation)*handL.rotation;
            if(handR!=null)restHandR=Quaternion.Inverse(transform.rotation)*handR.rotation;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                // Use the skin's bind pose, not whichever animation happened to evaluate first.
                var bones=r.bones;var bind=r.sharedMesh.bindposes;
                for(int i=0;i<bones.Length && i<bind.Length;i++)
                {
                    if(bones[i]==handL)restHandL=Quaternion.Inverse(transform.rotation)*(r.transform.localToWorldMatrix*bind[i].inverse).rotation;
                    if(bones[i]==handR)restHandR=Quaternion.Inverse(transform.rotation)*(r.transform.localToWorldMatrix*bind[i].inverse).rotation;
                }
                for (int i = 0; i < r.sharedMesh.blendShapeCount; i++)
                {
                    string name = r.sharedMesh.GetBlendShapeName(i);
                    if (name.EndsWith("Blink_L", StringComparison.Ordinal)) { lids = r; blinkLeft = i; }
                    if (name.EndsWith("Blink_R", StringComparison.Ordinal)) { lids = r; blinkRight = i; }
                    if(name.EndsWith("Grip_L",StringComparison.Ordinal)){fingerMesh=r;gripL=i;}
                    if(name.EndsWith("Grip_R",StringComparison.Ordinal)){fingerMesh=r;gripR=i;}
                }
            }
            random = new System.Random(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this));
            time = (float)random.NextDouble() * 6;
            nextBlink = time + 1 + (float)random.NextDouble() * 3;
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Play(Idle, 0, (float)random.NextDouble());
                state = Idle;
            }
        }

        public void Bind(WarehousePlayer player)
        {
            owner = player;
            previous = player.transform.position;
        }
        public bool PreviewToolPose(out Vector3 position,out Quaternion rotation)
        {
            position=transform.position;rotation=transform.rotation;
            if(handR==null)return false;
            rotation=handR.rotation*Quaternion.Inverse(restHandR)*Quaternion.Euler(90,0,0);
            position=handR.position+rotation*new Vector3(0,.055f,.04f);
            return true;
        }

        public void Greet()
        {
            if (animator == null || !animator.HasState(0, GreetState)) return;
            if (owner == null) { speed = 0; previewSpeed = 0; previewCarrying = false; }
            greetUntil = time + 2.4f;
            state = GreetState;
            animator.speed = 1;
            animator.CrossFadeInFixedTime(GreetState, .18f, 0, 0);
        }

        void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            float dt = Time.deltaTime;
            time += dt;
            bool carrying = previewCarrying;
            OpeningTool tool=owner!=null && owner.Grip!=null?owner.Grip.Tool:previewTool;
            float targetSpeed = previewSpeed;
            if (owner != null)
            {
                var delta = owner.transform.position - previous;
                previous = owner.transform.position;
                delta.y = 0;
                // A round-reset teleport must not produce a sprint pose.
                targetSpeed = dt > 0 && delta.sqrMagnitude < 1 ? Mathf.Min(delta.magnitude / dt, 4.5f) : 0;
                int kind;
                carrying = WarehouseWorld.Instance != null && WarehouseWorld.Instance.HeldCount(owner.OwnerClientId, out kind) > 0;
            }
            speed = Mathf.Lerp(speed, targetSpeed, 1 - Mathf.Exp(-10 * dt));
            bool moving = speed > .12f;
            if (moving || carrying) greetUntil = 0;
            int desired = time < greetUntil ? GreetState : carrying ? (moving ? CarryWalk : CarryIdle) : (moving ? Walk : Idle);
            if(tool!=OpeningTool.None)desired=tool==OpeningTool.BoxCutter?CutState:PryState;
            if (state != desired)
            {
                state = desired;
                animator.CrossFadeInFixedTime(desired, .2f, 0);
            }
            animator.speed = tool!=OpeningTool.None?1:moving ? Mathf.Clamp(speed / 2.3f, .45f, 1.65f) : 1;
        }

        void LateUpdate()
        {
            if (Time.deltaTime <= 0) return;
            var grip=owner!=null?owner.Grip:null;
            if(fingerMesh!=null)
            {
                bool working=grip!=null?grip.Tool!=OpeningTool.None:previewTool!=OpeningTool.None;
                bool carrying=grip!=null?grip.HeldCount>0:previewCarrying;
                if(gripL>=0)fingerMesh.SetBlendShapeWeight(gripL,working?35:carrying?25:0);
                if(gripR>=0)fingerMesh.SetBlendShapeWeight(gripR,working?100:carrying?25:0);
            }
            if(grip!=null && (grip.HeldCount>0 || grip.ToolWeight>0))
            {
                bool tool=grip.ToolWeight>0 && grip.HeldCount==0;
                Vector3 left=tool?grip.LeftToolWrist:grip.SupportWrist(true);
                Vector3 right=tool?grip.RightToolWrist:grip.SupportWrist(false);
                Quaternion frame=tool?grip.ToolRotation:grip.CarryAnchor.rotation;
                Quaternion palm=Quaternion.Euler(-90,0,0);
                float armWeight=tool && grip.Tool==OpeningTool.None?grip.ToolWeight:1;
                BrunoArmIK.Solve(upperL,lowerL,handL,left,transform.TransformPoint(new Vector3(-1,.75f,.1f)),frame*palm*restHandL,armWeight);
                BrunoArmIK.Solve(upperR,lowerR,handR,right,transform.TransformPoint(new Vector3(1,.75f,.1f)),frame*palm*restHandR,armWeight);
            }
            else if(grip==null && previewCarrying)
            {
                var palm=transform.rotation*Quaternion.Euler(-90,0,0);
                BrunoArmIK.Solve(upperL,lowerL,handL,transform.TransformPoint(new Vector3(-.147f,.97f,.47f)),transform.TransformPoint(new Vector3(-1,.75f,.1f)),palm*restHandL,1);
                BrunoArmIK.Solve(upperR,lowerR,handR,transform.TransformPoint(new Vector3(.147f,.97f,.47f)),transform.TransformPoint(new Vector3(1,.75f,.1f)),palm*restHandR,1);
            }
            // Animator writes Head each frame; these bounded offsets never accumulate.
            if (lookAround && head != null && animator != null && animator.isActiveAndEnabled)
            {
                float idleWeight = grip!=null && grip.Tool!=OpeningTool.None?0:1 - Mathf.Clamp01(speed / .8f);
                float yaw = Mathf.Sin(time * .61f) * Mathf.Sin(time * .19f) * 14 * idleWeight;
                float pitch = Mathf.Sin(time * .47f + 1.1f) * 3 * idleWeight;
                head.rotation = Quaternion.AngleAxis(yaw, transform.up) * Quaternion.AngleAxis(pitch, transform.right) * head.rotation;
            }
            if (lids == null) return;
            if (time >= nextBlink)
            {
                blinkStart = time;
                nextBlink = time + 2.6f + (float)random.NextDouble() * 3.5f;
            }
            float phase = (time - blinkStart) / .18f;
            float weight = phase >= 0 && phase < 1 ? Mathf.Sin(phase * Mathf.PI) * 100 : 0;
            if (blinkLeft >= 0) lids.SetBlendShapeWeight(blinkLeft, weight);
            if (blinkRight >= 0) lids.SetBlendShapeWeight(blinkRight, weight);
        }
    }
}
