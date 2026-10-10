using UnityEngine;

namespace Bidwarss
{
    // Procedural placeholder worker. Visual animation follows replicated player motion on every peer.
    public sealed class WarehouseAvatar : MonoBehaviour
    {
        Transform leftArm,rightArm,leftElbow,rightElbow,leftLeg,rightLeg,torso;
        float pulseStart=-10;
        int lastHeld;
        Vector3 previous;
        float gait,speed;
        WarehousePlayer player;
        Material material;
        public void Initialize(WarehousePlayer owner,Material shared)
        {
            player=owner;material=shared;previous=owner.transform.position;
            Color navy=new Color(.12f,.2f,.29f),orange=new Color(.96f,.48f,.12f),skin=new Color(.77f,.51f,.34f),dark=new Color(.07f,.09f,.12f);
            torso=Joint("Torso",transform,new Vector3(0,1.06f,0));
            Part("Jacket",torso,Vector3.zero,new Vector3(.58f,.55f,.33f),orange);
            Part("Overalls",torso,new Vector3(0,-.2f,-.015f),new Vector3(.5f,.2f,.35f),navy);
            Part("Reflective band",torso,new Vector3(0,.03f,.175f),new Vector3(.52f,.06f,.025f),new Color(1,.87f,.38f));
            Part("Head",transform,new Vector3(0,1.53f,0),new Vector3(.36f,.37f,.34f),skin);
            Part("Hard hat",transform,new Vector3(0,1.74f,0),new Vector3(.46f,.14f,.42f),new Color(1,.76f,.17f));
            Part("Brim",transform,new Vector3(0,1.68f,.065f),new Vector3(.49f,.045f,.5f),new Color(1,.76f,.17f));
            Part("Nose",transform,new Vector3(0,1.50f,.2f),new Vector3(.11f,.09f,.12f),skin);
            for(int sign=-1;sign<=1;sign+=2)
            {
                Part("Eye",transform,new Vector3(sign*.085f,1.59f,.174f),new Vector3(.042f,.045f,.018f),dark);
                var arm=Joint(sign<0?"Left arm":"Right arm",transform,new Vector3(sign*.36f,1.28f,0));
                Part("Sleeve",arm,new Vector3(0,-.12f,0),new Vector3(.17f,.27f,.2f),orange);
                // The elbow bends the forearm forward when carrying or heaving on a container door.
                var elbow=Joint(sign<0?"Left elbow":"Right elbow",arm,new Vector3(0,-.26f,0));
                Part("Forearm",elbow,new Vector3(0,-.07f,0),new Vector3(.16f,.17f,.19f),orange);
                Part("Glove",elbow,new Vector3(0,-.16f,.012f),new Vector3(.18f,.15f,.2f),dark);
                var leg=Joint(sign<0?"Left leg":"Right leg",transform,new Vector3(sign*.16f,.8f,0));
                Part("Trouser",leg,new Vector3(0,-.3f,0),new Vector3(.23f,.6f,.26f),navy);
                Part("Boot",leg,new Vector3(0,-.7f,.065f),new Vector3(.26f,.2f,.4f),dark);
                if(sign<0){leftArm=arm;leftElbow=elbow;leftLeg=leg;}else{rightArm=arm;rightElbow=elbow;rightLeg=leg;}
            }
        }
        static Transform Joint(string name,Transform parent,Vector3 pos)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}
        void Part(string name,Transform parent,Vector3 pos,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=size;
            var col=go.GetComponent<Collider>();col.enabled=false;Destroy(col);
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);renderer.SetPropertyBlock(block);
        }
        void LateUpdate()
        {
            if(player==null||torso==null)return;
            var delta=player.transform.position-previous;delta.y=0;previous=player.transform.position;
            float measured=Time.deltaTime>0?delta.magnitude/Time.deltaTime:0;
            speed=Mathf.Lerp(speed,Mathf.Min(measured,4.5f),1-Mathf.Exp(-12*Time.deltaTime));
            gait+=speed*Time.deltaTime*2.7f;
            float swing=Mathf.Sin(gait)*Mathf.Min(1,speed/2)*27;
            leftLeg.localRotation=Quaternion.Euler(swing,0,0);rightLeg.localRotation=Quaternion.Euler(-swing,0,0);
            var world=WarehouseWorld.Instance;int kind;
            int held=world!=null?world.HeldCount(player.OwnerClientId,out kind):0;
            bool carrying=held>0;
            if(held!=lastHeld){pulseStart=Time.time;lastHeld=held;}
            // Straining against a container door: arms out front, heaving back and forth.
            float effort=0;
            if(world!=null)
                for(int i=0;i<world.Crates.Count;i++)
                {
                    var crate=world.Crates[i];
                    if(crate.active&&!crate.opened&&crate.opener==player.OwnerClientId&&crate.progress>0){effort=crate.progress;break;}
                }
            float pulse=Mathf.Sin(Mathf.PI*Mathf.Clamp01((Time.time-pulseStart)/.4f));
            float shoulderL=-swing,shoulderR=swing,elbowBend=-8;
            float rollL=5,rollR=-5;
            if(carrying){shoulderL=shoulderR=-52;elbowBend=-58;rollL=-12;rollR=12;}
            if(effort>0)
            {
                float heave=Mathf.Sin(Time.time*13)*18*effort;
                shoulderL=shoulderR=-78+heave;elbowBend=-24-heave*.8f;rollL=-6;rollR=6;
            }
            // A quick reach whenever something is picked up or put down.
            shoulderL-=pulse*38;shoulderR-=pulse*38;
            var left=Quaternion.Euler(shoulderL,0,rollL);
            var right=Quaternion.Euler(shoulderR,0,rollR);
            float t=1-Mathf.Exp(-12*Time.deltaTime);
            leftArm.localRotation=Quaternion.Slerp(leftArm.localRotation,left,t);rightArm.localRotation=Quaternion.Slerp(rightArm.localRotation,right,t);
            var bend=Quaternion.Euler(elbowBend,0,0);
            leftElbow.localRotation=Quaternion.Slerp(leftElbow.localRotation,bend,t);rightElbow.localRotation=Quaternion.Slerp(rightElbow.localRotation,bend,t);
            torso.localPosition=new Vector3(0,1.06f+Mathf.Abs(Mathf.Sin(gait))*.025f*Mathf.Min(1,speed),0);
        }
    }
}
