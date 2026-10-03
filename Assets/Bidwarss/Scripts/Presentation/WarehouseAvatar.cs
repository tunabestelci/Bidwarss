using UnityEngine;

namespace Bidwarss
{
    // Bruno follows replicated movement. The old worker remains a missing-asset fallback.
    public sealed class WarehouseAvatar : MonoBehaviour
    {
        Transform leftArm,rightArm,leftLeg,rightLeg,torso;
        Vector3 previous;
        float gait,speed;
        WarehousePlayer player;
        Material material;
        BrunoMotion bruno;
        public void Initialize(WarehousePlayer owner,Material shared)
        {
            player=owner;material=shared;previous=owner.transform.position;
            var character=Resources.Load<GameObject>("Bruno/BrunoCharacter");
            if(character!=null)
            {
                var instance=Instantiate(character,transform,false);
                bruno=instance.GetComponent<BrunoMotion>();
                if(bruno!=null){bruno.Bind(owner);return;}
                Destroy(instance);
            }
            Debug.LogWarning("Bruno prefab bulunamadi. Bidwarss > Bruno > Build Character Assets komutunu calistirin.",this);
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
                Part("Sleeve",arm,new Vector3(0,-.15f,0),new Vector3(.17f,.34f,.2f),orange);
                Part("Glove",arm,new Vector3(0,-.39f,.025f),new Vector3(.18f,.17f,.2f),dark);
                var leg=Joint(sign<0?"Left leg":"Right leg",transform,new Vector3(sign*.16f,.8f,0));
                Part("Trouser",leg,new Vector3(0,-.3f,0),new Vector3(.23f,.6f,.26f),navy);
                Part("Boot",leg,new Vector3(0,-.7f,.065f),new Vector3(.26f,.2f,.4f),dark);
                if(sign<0){leftArm=arm;leftLeg=leg;}else{rightArm=arm;rightLeg=leg;}
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
            if(bruno!=null||player==null||torso==null)return;
            var delta=player.transform.position-previous;delta.y=0;previous=player.transform.position;
            float measured=Time.deltaTime>0?delta.magnitude/Time.deltaTime:0;
            speed=Mathf.Lerp(speed,Mathf.Min(measured,4.5f),1-Mathf.Exp(-12*Time.deltaTime));
            gait+=speed*Time.deltaTime*2.7f;
            float swing=Mathf.Sin(gait)*Mathf.Min(1,speed/2)*27;
            leftLeg.localRotation=Quaternion.Euler(swing,0,0);rightLeg.localRotation=Quaternion.Euler(-swing,0,0);
            var world=WarehouseWorld.Instance;int kind;
            bool carrying=world!=null&&world.HeldCount(player.OwnerClientId,out kind)>0;
            var left=Quaternion.Euler(carrying?-65:-swing,0,carrying?-12:5);
            var right=Quaternion.Euler(carrying?-65:swing,0,carrying?12:-5);
            float t=1-Mathf.Exp(-12*Time.deltaTime);
            leftArm.localRotation=Quaternion.Slerp(leftArm.localRotation,left,t);rightArm.localRotation=Quaternion.Slerp(rightArm.localRotation,right,t);
            torso.localPosition=new Vector3(0,1.06f+Mathf.Abs(Mathf.Sin(gait))*.025f*Mathf.Min(1,speed),0);
        }
    }
}
