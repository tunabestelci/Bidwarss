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
        float bobPhase;
        public void Initialize(WarehousePlayer owner,Material material)
        {
            player=owner;
            var prefab=Resources.Load<GameObject>("Bruno/BrunoFirstPersonHands");
            if(prefab!=null)
            {
                var instance=Instantiate(prefab,transform,false);
                foreach(var t in instance.GetComponentsInChildren<Transform>())
                {if(t.name=="HandLeft")left=t;else if(t.name=="HandRight")right=t;}
                if(left!=null && right!=null){BrunoOutline.Cap(instance,BrunoOutline.HandWidth);brunoHands=true;left=Pivot(left);right=Pivot(right);skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>();return;}
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
        // Smooth hand poses: idle (breathing, walking sway, reaching toward what you look at) blends into the carry
        // and tool poses instead of snapping, and the finger grip ramps up/down instead of jumping.
        Vector3 idleL,idleR;Quaternion idleLRot=Quaternion.identity,idleRRot=Quaternion.identity;
        bool idleReady;
        float carryBlend,toolBlend,reach,gripLeft,gripRight;
        struct GripShape{public SkinnedMeshRenderer skin;public int index;public bool right;}
        GripShape[] gripShapes;
        static float Ease(float t){return t*t*(3-2*t);}
        void CacheGripShapes()
        {
            var list=new System.Collections.Generic.List<GripShape>();
            if(skins!=null)foreach(var skin in skins)
            {
                if(skin==null||skin.sharedMesh==null)continue;
                for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)
                    if(skin.sharedMesh.GetBlendShapeName(i).EndsWith("Grip"))list.Add(new GripShape{skin=skin,index=i,right=skin.name.Contains("Right")});
            }
            gripShapes=list.ToArray();
        }
        void LateUpdate()
        {
            if(player==null||left==null)return;
            if(gripShapes==null)CacheGripShapes();
            var rig=player.Grip;
            bool carry=rig!=null && rig.HeldCount>0;
            bool working=rig!=null && rig.Tool!=Bidwarss.Domain.OpeningTool.None;
            float dt=Time.deltaTime;
            toolBlend=Mathf.MoveTowards(toolBlend,working?1:0,dt*7f);
            carryBlend=Mathf.MoveTowards(carryBlend,carry&&!working?1:0,dt*6f);
            var looked=player.Looked;
            bool aim=looked!=null&&!carry&&!working&&(looked.kind==TargetKind.Item||looked.kind==TargetKind.Box);
            reach=Mathf.MoveTowards(reach,aim?1:0,dt*5f);
            float x=brunoHands?.25f:.29f,y=brunoHands?-.28f:-.34f,z=brunoHands?.44f:.48f;
            float breath=brunoHands?Mathf.Sin(Time.time*2)*.003f:0;
            bobPhase+=player.PlanarSpeed*dt*1.8f;
            float speed01=Mathf.Clamp01(player.PlanarSpeed/WarehousePlayer.WalkSpeed);
            if(WarehousePlayer.HeadBob)breath-=Mathf.Abs(Mathf.Sin(bobPhase))*.012f*speed01;
            float sway=Mathf.Sin(bobPhase*.5f)*.014f*speed01+Mathf.Sin(Time.time*1.3f)*.003f;
            float re=Ease(reach);
            // idle target, hands drift toward the middle and forward when you look at something you can grab or open
            Vector3 targetL=new Vector3(-x+sway+re*.045f,y+breath+re*.03f,z+re*.07f);
            Vector3 targetR=new Vector3(x+sway-re*.045f,y+breath+re*.03f-Mathf.Sin(Time.time*1.7f)*.002f,z+re*.07f);
            Quaternion rotL=Quaternion.Euler(-12f*re,0,-7f*re+sway*60f),rotR=Quaternion.Euler(-12f*re,0,7f*re+sway*60f);
            float t=1-Mathf.Exp(-12*dt);
            if(!idleReady){idleL=targetL;idleR=targetR;idleLRot=rotL;idleRRot=rotR;idleReady=true;}
            idleL=Vector3.Lerp(idleL,targetL,t);idleR=Vector3.Lerp(idleR,targetR,t);
            idleLRot=Quaternion.Slerp(idleLRot,rotL,t);idleRRot=Quaternion.Slerp(idleRRot,rotR,t);
            Vector3 posL=transform.TransformPoint(idleL),posR=transform.TransformPoint(idleR);
            Quaternion qL=transform.rotation*idleLRot,qR=transform.rotation*idleRRot;
            if(carryBlend>0.001f&&rig!=null)
            {
                float b=Ease(carryBlend);
                posL=Vector3.Lerp(posL,rig.SupportWrist(true),b);posR=Vector3.Lerp(posR,rig.SupportWrist(false),b);
                qL=Quaternion.Slerp(qL,rig.CarryAnchor.rotation,b);qR=Quaternion.Slerp(qR,rig.CarryAnchor.rotation,b);
            }
            if(toolBlend>0.001f&&rig!=null)
            {
                float b=Ease(toolBlend);
                posL=Vector3.Lerp(posL,rig.LeftToolWrist,b);posR=Vector3.Lerp(posR,rig.RightToolWrist,b);
                qL=Quaternion.Slerp(qL,rig.ToolRotation,b);qR=Quaternion.Slerp(qR,rig.ToolRotation,b);
            }
            left.SetPositionAndRotation(posL,qL);
            right.SetPositionAndRotation(posR,qR);
            // fingers: relaxed -> half open when reaching -> closed around the handle / the item
            float gl=working?35:carry?25:re*12f,gr=working?100:carry?25:re*12f;
            float g=1-Mathf.Exp(-14*dt);
            gripLeft=Mathf.Lerp(gripLeft,gl,g);gripRight=Mathf.Lerp(gripRight,gr,g);
            if(gripShapes!=null)foreach(var shape in gripShapes)
                if(shape.skin!=null)shape.skin.SetBlendShapeWeight(shape.index,shape.right?gripRight:gripLeft);
        }
    }
}
