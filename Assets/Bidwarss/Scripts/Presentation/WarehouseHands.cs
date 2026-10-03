using UnityEngine;
namespace Bidwarss
{
    [DefaultExecutionOrder(100)]
    public sealed class WarehouseHands : MonoBehaviour
    {
        Transform left,right;
        WarehousePlayer player;
        bool brunoHands;
        SkinnedMeshRenderer[] skins;
        public void Initialize(WarehousePlayer owner,Material material)
        {
            player=owner;
            var prefab=Resources.Load<GameObject>("Bruno/BrunoFirstPersonHands");
            if(prefab!=null)
            {
                var instance=Instantiate(prefab,transform,false);
                foreach(var t in instance.GetComponentsInChildren<Transform>())
                {if(t.name=="HandLeft")left=t;else if(t.name=="HandRight")right=t;}
                if(left!=null && right!=null){brunoHands=true;left=Pivot(left);right=Pivot(right);skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>();return;}
                Destroy(instance);left=null;right=null;
            }
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
            left=Pivot(left);right=Pivot(right);
        }
        static Transform Pivot(Transform mesh)
        {
            var pivot=new GameObject(mesh.name+" wrist").transform;
            pivot.SetParent(mesh.parent,false);pivot.localPosition=mesh.localPosition;
            mesh.SetParent(pivot,false);mesh.localPosition=Vector3.zero;
            return pivot;
        }
        void LateUpdate()
        {
            if(player==null||left==null)return;
            var rig=player.Grip;
            bool carry=rig!=null && rig.HeldCount>0;
            bool working=rig!=null && rig.Tool!=Bidwarss.Domain.OpeningTool.None;
            float x=brunoHands?.25f:.29f,y=brunoHands?-.28f:-.34f;
            float breath=brunoHands?Mathf.Sin(Time.time*2)*.003f:0;
            float z=brunoHands?.44f:.48f;
            if(working)
            {
                left.SetPositionAndRotation(rig.LeftToolWrist,rig.ToolRotation);
                right.SetPositionAndRotation(rig.RightToolWrist,rig.ToolRotation);
            }
            else if(carry)
            {
                left.SetPositionAndRotation(rig.SupportWrist(true),rig.CarryAnchor.rotation);
                right.SetPositionAndRotation(rig.SupportWrist(false),rig.CarryAnchor.rotation);
            }
            else
            {
                float t=1-Mathf.Exp(-15*Time.deltaTime);
                left.localPosition=Vector3.Lerp(left.localPosition,new Vector3(-x,y+breath,z),t);
                right.localPosition=Vector3.Lerp(right.localPosition,new Vector3(x,y+breath,z),t);
                left.localRotation=Quaternion.Slerp(left.localRotation,Quaternion.identity,t);
                right.localRotation=Quaternion.Slerp(right.localRotation,Quaternion.identity,t);
            }
            if(skins!=null)foreach(var skin in skins)
                for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                    if(skin.sharedMesh.GetBlendShapeName(i).EndsWith("Grip"))skin.SetBlendShapeWeight(i,working?(skin.name.Contains("Right")?100:35):carry?25:0);
        }
    }
}
