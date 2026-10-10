using UnityEngine;
using UnityEngine.InputSystem;

namespace Bidwarss
{
    // First-person gloved hands, built from cubes. Everything is procedural:
    //  - idle breathing and a walking bob that follows the real movement speed,
    //  - a carry pose with the fingers hooked under the load,
    //  - a reach-and-grab lunge when taking a piece, a push when stacking or dropping,
    //  - hands that grip the container doors and strain against them while the crate opens, then fling them wide.
    public sealed class WarehouseHands : MonoBehaviour
    {
        enum Gesture { None, Grab, Release, Fling }

        sealed class Hand
        {
            public float side;
            public Transform root;
            public Transform[] proximal=new Transform[4],distal=new Transform[4];
            public Transform thumbBase,thumbTip;
            public Vector3 position;
            public Quaternion rotation=Quaternion.identity;
            public float curl;
        }

        Hand left,right;
        WarehousePlayer player;
        Material material;
        Vector3 lastPlayerPosition;
        float speed,gait;
        Gesture gesture;
        float gestureStart;
        bool gripped;
        int lastHeld;
        int openingCrate=-1;

        public void Initialize(WarehousePlayer owner,Material shared)
        {
            player=owner;material=shared;lastPlayerPosition=owner.transform.position;
            left=BuildHand(-1);right=BuildHand(1);
        }

        Hand BuildHand(int side)
        {
            var hand=new Hand{side=side};
            var skin=new Color(.12f,.18f,.22f);       // work glove
            var jacket=new Color(.96f,.48f,.12f);
            var band=new Color(1f,.87f,.38f);
            hand.root=Joint("Hand "+(side<0?"L":"R"),transform,new Vector3(side*.27f,-.33f,.46f));
            Part("Palm",hand.root,new Vector3(0,0,0),new Vector3(.095f,.034f,.1f),skin);
            Part("Knuckle pad",hand.root,new Vector3(0,.019f,.035f),new Vector3(.09f,.012f,.03f),new Color(.2f,.28f,.33f));
            float[] offsets={-.036f,-.012f,.012f,.036f};
            float[] lengths={.88f,1f,.95f,.78f};
            for(int i=0;i<4;i++)
            {
                float length=lengths[i];
                var proximal=Joint("Finger "+i,hand.root,new Vector3(offsets[i],0,.05f));
                Part("Segment",proximal,new Vector3(0,0,.022f*length),new Vector3(.021f,.021f,.044f*length),skin);
                var distal=Joint("Fingertip",proximal,new Vector3(0,0,.044f*length));
                Part("Tip",distal,new Vector3(0,0,.019f*length),new Vector3(.019f,.019f,.038f*length),skin);
                hand.proximal[i]=proximal;hand.distal[i]=distal;
            }
            // The thumb sits on the inner side of the hand and sweeps across the palm when it curls.
            hand.thumbBase=Joint("Thumb",hand.root,new Vector3(-side*.05f,0,-.005f));
            hand.thumbBase.localRotation=Quaternion.Euler(0,-side*38,0);
            Part("Thumb segment",hand.thumbBase,new Vector3(0,0,.022f),new Vector3(.024f,.024f,.046f),skin);
            hand.thumbTip=Joint("Thumb tip",hand.thumbBase,new Vector3(0,0,.046f));
            Part("Thumb end",hand.thumbTip,new Vector3(0,0,.018f),new Vector3(.021f,.021f,.036f),skin);
            // The jacket sleeve runs back toward an elbow below the camera, with a reflective cuff.
            Part("Cuff",hand.root,new Vector3(0,-.002f,-.075f),new Vector3(.105f,.1f,.03f),band);
            Part("Work jacket forearm",hand.root,new Vector3(0,-.02f,-.27f),new Vector3(.1f,.095f,.36f),jacket);
            hand.position=hand.root.localPosition;
            return hand;
        }

        static Transform Joint(string name,Transform parent,Vector3 local)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}

        void Part(string name,Transform parent,Vector3 local,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=local;go.transform.localScale=size;
            var col=go.GetComponent<Collider>();col.enabled=false;Destroy(col);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);
        }

        void StartGesture(Gesture kind)
        {
            // A new gesture never interrupts a younger one of the same kind (E can be mashed).
            if(gesture==kind&&Time.time-gestureStart<.25f)return;
            gesture=kind;gestureStart=Time.time;gripped=false;
        }

        // The crate this player is currently straining open, or -1.
        static int CrateBeingOpened(WarehouseWorld world,ulong client,out float progress)
        {
            progress=0;
            for(int i=0;i<world.Crates.Count;i++)
            {
                var crate=world.Crates[i];
                if(crate.active&&!crate.opened&&crate.opener==client&&crate.progress>0){progress=crate.progress;return i;}
            }
            return -1;
        }

        void LateUpdate()
        {
            if(player==null||left==null)return;
            var world=WarehouseWorld.Instance;
            float dt=Mathf.Min(Time.deltaTime,.05f);

            // Walking speed from the real displacement, smoothed.
            var delta=player.transform.position-lastPlayerPosition;delta.y=0;lastPlayerPosition=player.transform.position;
            float measured=dt>0?delta.magnitude/dt:0;
            speed=Mathf.Lerp(speed,Mathf.Min(measured,5f),1-Mathf.Exp(-10*dt));
            float stride=Mathf.Min(1,speed/3.2f);
            gait+=speed*dt*2.4f;

            int kind;int held=world!=null?world.HeldCount(player.OwnerClientId,out kind):0;
            bool carry=held>0;
            float progress=0;int crateIndex=world!=null?CrateBeingOpened(world,player.OwnerClientId,out progress):-1;
            bool opening=crateIndex>=0;

            // Gestures: start on the key press so the hands react at once, with the replicated state as a backstop.
            var keyboard=Keyboard.current;var pad=Gamepad.current;
            bool pressed=(keyboard!=null&&keyboard.eKey.wasPressedThisFrame)||(pad!=null&&pad.buttonSouth.wasPressedThisFrame);
            bool dropped=(keyboard!=null&&keyboard.qKey.wasPressedThisFrame)||(pad!=null&&pad.buttonEast.wasPressedThisFrame);
            var looked=player.Looked;
            if(pressed&&looked!=null&&Cursor.lockState==CursorLockMode.Locked)
            {
                if(looked.kind==TargetKind.Item)StartGesture(Gesture.Grab);
                else if(looked.kind==TargetKind.Slot&&carry)StartGesture(Gesture.Release);
            }
            if(dropped&&carry)StartGesture(Gesture.Release);
            if(held>lastHeld&&gesture!=Gesture.Grab)StartGesture(Gesture.Grab);
            if(held<lastHeld&&gesture!=Gesture.Release)StartGesture(Gesture.Release);
            lastHeld=held;
            // The crate this player was straining open has just given way: fling the doors wide.
            if(crateIndex>=0)openingCrate=crateIndex;
            else if(openingCrate>=0)
            {
                if(world!=null&&openingCrate<world.Crates.Count&&world.Crates[openingCrate].opened)StartGesture(Gesture.Fling);
                openingCrate=-1;
            }

            float t=gesture==Gesture.None?0:Time.time-gestureStart;
            float duration=gesture==Gesture.Grab?.46f:gesture==Gesture.Release?.4f:gesture==Gesture.Fling?.6f:0;
            if(gesture!=Gesture.None&&t>=duration)gesture=Gesture.None;
            float u=duration>0?Mathf.Clamp01(t/duration):0;

            ApplyPose(left,carry,opening,progress,stride,u,dt);
            ApplyPose(right,carry,opening,progress,stride,u,dt);
        }

        void ApplyPose(Hand hand,bool carry,bool opening,float progress,float stride,float u,float dt)
        {
            float s=hand.side;
            // Base pose.
            Vector3 position=new Vector3(s*.27f,-.33f,.46f);
            Quaternion rotation=Quaternion.Euler(8,-s*6,-s*4);
            float curl=.22f;
            if(carry)
            {
                // Cradling a load: hands closer together and rolled inward, fingers hooked underneath.
                position=new Vector3(s*.21f,-.25f,.5f);
                rotation=Quaternion.Euler(-16,-s*14,-s*20);
                curl=.62f;
            }
            if(opening)
            {
                // Hands on the door handles, leaning back with the effort.
                float strain=Mathf.Sin(Time.time*13+s)*.025f*progress;
                position=new Vector3(s*.2f,-.1f+strain*.4f,.56f-strain-progress*.04f);
                rotation=Quaternion.Euler(-24,-s*8,-s*8);
                curl=.92f;
            }
            // Idle breathing and the walking bob.
            float breathe=Mathf.Sin(Time.time*1.7f+s)*.004f;
            position.y+=breathe+Mathf.Abs(Mathf.Sin(gait))*.016f*stride;
            position.x+=Mathf.Cos(gait)*.012f*stride*s;
            position.z+=Mathf.Sin(gait*2)*.006f*stride;
            rotation*=Quaternion.Euler(Mathf.Sin(gait*2)*3*stride,0,Mathf.Cos(gait)*4*stride*s);

            // Smooth toward the pose, then add the gesture on top so it stays snappy.
            float blend=1-Mathf.Exp(-14*dt);
            hand.position=Vector3.Lerp(hand.position,position,blend);
            hand.rotation=Quaternion.Slerp(hand.rotation,rotation,blend);
            hand.curl=Mathf.Lerp(hand.curl,curl,1-Mathf.Exp(-18*dt));

            Vector3 offset=Vector3.zero;Quaternion extra=Quaternion.identity;float shownCurl=hand.curl;
            if(gesture==Gesture.Grab)
            {
                // Lunge forward with open fingers, snap shut at the top of the reach, pull back.
                float reach=Mathf.Sin(Mathf.PI*Mathf.Pow(u,.75f));
                offset=new Vector3(-s*.05f*reach,.045f*reach,.22f*reach);
                extra=Quaternion.Euler(-14*reach,0,0);
                float closing=Mathf.Clamp01((u-.42f)/.14f);
                float weight=u<.8f?1:1-(u-.8f)/.2f;
                shownCurl=Mathf.Lerp(hand.curl,Mathf.Lerp(.06f,1f,closing),weight);
                if(!gripped&&u>=.5f){gripped=true;RevealEffects.Grip(transform.position+transform.forward*.5f);}
            }
            else if(gesture==Gesture.Release)
            {
                float push=Mathf.Sin(Mathf.PI*u);
                offset=new Vector3(s*.025f*push,-.04f*push,.12f*push);
                extra=Quaternion.Euler(10*push,0,0);
                shownCurl=Mathf.Lerp(hand.curl,.05f,Mathf.Clamp01(u*3)*(1-Mathf.Clamp01((u-.7f)/.3f)));
            }
            else if(gesture==Gesture.Fling)
            {
                float swing=Mathf.Sin(Mathf.PI*u);
                offset=new Vector3(s*.2f*swing,.1f*swing,.08f*swing);
                extra=Quaternion.Euler(-18*swing,s*26*swing,s*18*swing);
                shownCurl=Mathf.Lerp(hand.curl,.08f,swing);
            }

            hand.root.localPosition=hand.position+offset;
            hand.root.localRotation=hand.rotation*extra;
            SetCurl(hand,shownCurl);
        }

        static void SetCurl(Hand hand,float curl)
        {
            for(int i=0;i<4;i++)
            {
                // Outer fingers close a little later than the index finger, like a real fist.
                float c=Mathf.Clamp01(curl*(1.08f-i*.05f));
                hand.proximal[i].localRotation=Quaternion.Euler(c*72,0,0);
                hand.distal[i].localRotation=Quaternion.Euler(c*88,0,0);
            }
            hand.thumbBase.localRotation=Quaternion.Euler(curl*18,-hand.side*(38-curl*22),0);
            hand.thumbTip.localRotation=Quaternion.Euler(curl*35,0,0);
        }
    }
}
