using UnityEngine;

namespace Bidwarss
{
    // Procedural placeholder worker. Visual animation follows replicated player motion on every peer.
    public sealed class WarehouseAvatar : MonoBehaviour
    {
        Transform leftArm,rightArm,leftElbow,rightElbow,leftLeg,rightLeg,torso;
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
            var head=Joint("Head pivot",transform,Vector3.zero);
            Part("Head",head,new Vector3(0,1.53f,0),new Vector3(.36f,.37f,.34f),skin);
            Part("Hard hat",head,new Vector3(0,1.74f,0),new Vector3(.46f,.14f,.42f),new Color(1,.76f,.17f));
            Part("Brim",head,new Vector3(0,1.68f,.065f),new Vector3(.49f,.045f,.5f),new Color(1,.76f,.17f));
            Part("Nose",head,new Vector3(0,1.50f,.2f),new Vector3(.11f,.09f,.12f),skin);
            for(int sign=-1;sign<=1;sign+=2)
            {
                Part("Eye",head,new Vector3(sign*.085f,1.59f,.174f),new Vector3(.042f,.045f,.018f),dark);
                var arm=Joint(sign<0?"Left arm":"Right arm",transform,new Vector3(sign*.36f,1.28f,0));
                Part("Upper sleeve",arm,new Vector3(0,-.135f,0),new Vector3(.17f,.29f,.2f),orange);
                var elbow=Joint("Elbow",arm,new Vector3(0,-.27f,0));
                Part("Forearm sleeve",elbow,new Vector3(0,-.115f,0),new Vector3(.155f,.25f,.18f),orange);
                Part("Glove",elbow,new Vector3(0,-.285f,0),new Vector3(.18f,.16f,.2f),dark);
                var leg=Joint(sign<0?"Left leg":"Right leg",transform,new Vector3(sign*.16f,.8f,0));
                Part("Trouser",leg,new Vector3(0,-.3f,0),new Vector3(.23f,.6f,.26f),navy);
                Part("Boot",leg,new Vector3(0,-.7f,.065f),new Vector3(.26f,.2f,.4f),dark);
                if(sign<0){leftArm=arm;leftElbow=elbow;leftLeg=leg;}else{rightArm=arm;rightElbow=elbow;rightLeg=leg;}
            }
        }
        public void SetFirstPerson(bool firstPerson)
        {
            var head=transform.Find("Head pivot");
            if(head==null)return;
            foreach(var renderer in head.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode=firstPerson
                    ?UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    :UnityEngine.Rendering.ShadowCastingMode.On;
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
            bool carrying=world!=null&&world.HeldCount(player.OwnerClientId,out kind)>0;
            var left=Quaternion.Euler(carrying?-25:-swing,0,carrying?-12:5);
            var right=Quaternion.Euler(carrying?-25:swing,0,carrying?12:-5);
            float t=1-Mathf.Exp(-12*Time.deltaTime);
            leftArm.localRotation=Quaternion.Slerp(leftArm.localRotation,left,t);rightArm.localRotation=Quaternion.Slerp(rightArm.localRotation,right,t);
            var elbowPose=Quaternion.Euler(carrying?-70:-12,0,0);
            leftElbow.localRotation=Quaternion.Slerp(leftElbow.localRotation,elbowPose,t);
            rightElbow.localRotation=Quaternion.Slerp(rightElbow.localRotation,elbowPose,t);
            if(player.IsOwner)transform.rotation=Quaternion.Euler(0,player.ViewYaw,0);
            torso.localPosition=new Vector3(0,1.06f+Mathf.Abs(Mathf.Sin(gait))*.025f*Mathf.Min(1,speed),0);
        }
    }
}
