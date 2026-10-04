using UnityEngine;
namespace Bidwarss
{
    public sealed class WarehouseHands : MonoBehaviour
    {
        Transform left,right;
        WarehousePlayer player;
        public void Initialize(WarehousePlayer owner,Material material)
        {
            player=owner;
            for(int sign=-1;sign<=1;sign+=2)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Work glove";go.transform.SetParent(transform,false);
                go.transform.localScale=new Vector3(.11f,.12f,.2f);
                go.transform.localPosition=new Vector3(sign*.29f,-.34f,.48f);
                var col=go.GetComponent<Collider>();col.enabled=false;Destroy(col);
                var r=go.GetComponent<Renderer>();r.sharedMaterial=material;
                var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(.12f,.18f,.22f));r.SetPropertyBlock(block);
                // The sleeve extends behind the glove toward an elbow below the camera.
                var sleeve=GameObject.CreatePrimitive(PrimitiveType.Cube);
                sleeve.name="Work jacket forearm";sleeve.transform.SetParent(go.transform,false);
                // Cancel the glove scale: dimensions below are expressed in camera metres.
                sleeve.transform.localScale=new Vector3(.105f/.11f,.105f/.12f,.38f/.2f);
                sleeve.transform.localPosition=new Vector3(0,-.025f/.12f,-.25f/.2f);
                var sleeveCollider=sleeve.GetComponent<Collider>();sleeveCollider.enabled=false;Destroy(sleeveCollider);
                var sleeveRenderer=sleeve.GetComponent<Renderer>();sleeveRenderer.sharedMaterial=material;
                var sleeveColor=new MaterialPropertyBlock();sleeveColor.SetColor("_BaseColor",new Color(.96f,.48f,.12f));
                sleeveRenderer.SetPropertyBlock(sleeveColor);
                if(sign<0)left=go.transform;else right=go.transform;
            }
        }
        void LateUpdate()
        {
            if(player==null||left==null)return;
            var world=WarehouseWorld.Instance;int kind;
            bool carry=world!=null&&world.HeldCount(player.OwnerClientId,out kind)>0;
            float x=carry?.23f:.29f,y=carry?-.22f:-.34f;
            left.localPosition=Vector3.Lerp(left.localPosition,new Vector3(-x,y,.48f),1-Mathf.Exp(-15*Time.deltaTime));
            right.localPosition=Vector3.Lerp(right.localPosition,new Vector3(x,y,.48f),1-Mathf.Exp(-15*Time.deltaTime));
        }
    }
}
