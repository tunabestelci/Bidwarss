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
