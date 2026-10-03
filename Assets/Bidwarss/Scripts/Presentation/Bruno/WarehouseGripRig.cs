using Bidwarss.Domain;
using UnityEngine;

namespace Bidwarss
{
    // Runs before arm IK and before WarehouseWorld places the held item visuals.
    [DefaultExecutionOrder(10)]
    public sealed class WarehouseGripRig : MonoBehaviour
    {
        public Transform CarryAnchor { get; private set; }
        public int HeldCount { get; private set; }
        public OpeningTool Tool { get; private set; }
        public float ToolPhase { get; private set; }
        public Vector3 LeftToolWrist { get; private set; }
        public Vector3 RightToolWrist { get; private set; }
        public Quaternion ToolRotation { get; private set; }
        public float ToolWeight { get; private set; }
        WarehousePlayer owner;
        Transform cameraTransform;
        GameObject cutter, prybar;
        Transform cutterTip, pryTip;
        OpeningTool previousTool;
        Vector3 equipRight,equipLeft;
        Quaternion equipRotation;
        static readonly Vector3 ToolInPalm = new Vector3(0,.055f,.04f);
        public void Initialize(WarehousePlayer player)
        {
            owner = player;
            CarryAnchor = new GameObject("Carry support plane").transform;
            CarryAnchor.SetParent(transform,false);
            CarryAnchor.localPosition = new Vector3(0,1.04f,.54f);
            cutter = CreateTool("Bruno/BoxCutterTool", out cutterTip);
            prybar = CreateTool("Bruno/PryBarTool", out pryTip);
        }
        GameObject CreateTool(string path,out Transform tip)
        {
            tip=null;
            var prefab=Resources.Load<GameObject>(path);
            if(prefab==null){Debug.LogWarning("Bruno tool missing: "+path+". Run Build Character Assets.",this);return null;}
            var instance=Instantiate(prefab,transform,false);
            foreach(var t in instance.GetComponentsInChildren<Transform>()) if(t.name=="ToolTip")tip=t;
            instance.SetActive(false);return instance;
        }
        public void UseCamera(Transform eye)
        {
            cameraTransform=eye;
            CarryAnchor.SetParent(eye,false);
            CarryAnchor.localPosition=new Vector3(0,-.38f,.62f);
        }
        void LateUpdate()
        {
            var world=WarehouseWorld.Instance;
            if(owner==null || world==null || !owner.IsSpawned)return;
            int kind; HeldCount=world.HeldCount(owner.OwnerClientId,out kind);
            float breath=Mathf.Sin(Time.time*2.1f)*.003f;
            CarryAnchor.localPosition=cameraTransform!=null?new Vector3(0,-.38f+breath,.62f):new Vector3(0,1.04f+breath,.54f);
            CrateState state=default;OpeningMode mode=OpeningMode.Hands;
            bool active=HeldCount==0 && world.TryOpening(owner.OwnerClientId,out state,out mode);
            Vector3 oldRight=RightToolWrist,oldLeft=LeftToolWrist;Quaternion oldRotation=ToolRotation;
            Tool=OpeningTool.None;
            if(active)
            {
                Tool=OpeningSequence.Sample(mode,state.progress,out float phase);ToolPhase=phase;
                Vector3 point=state.contactPoint,normal=state.contactNormal;
                if(normal.sqrMagnitude<.5f)normal=-transform.forward;
                normal.Normalize();
                Vector3 up=Vector3.ProjectOnPlane(Vector3.up,normal);
                if(up.sqrMagnitude<.01f)up=Vector3.ProjectOnPlane(transform.forward,normal);
                Quaternion facing=Quaternion.LookRotation(-normal,up.normalized);
                Vector3 across=facing*Vector3.right;
                if(Tool==OpeningTool.BoxCutter)point+=across*Mathf.Lerp(-.09f,.09f,phase);
                float lever=Tool==OpeningTool.PryBar?Mathf.Lerp(-12,32,Mathf.SmoothStep(0,1,phase)):4;
                ToolRotation=facing*Quaternion.Euler(lever,0,0);
                var item=Tool==OpeningTool.BoxCutter?cutter:prybar;
                var tip=Tool==OpeningTool.BoxCutter?cutterTip:pryTip;
                Vector3 tipLocal=item!=null && tip!=null?item.transform.InverseTransformPoint(tip.position):new Vector3(0,0,Tool==OpeningTool.PryBar?.37f:.21f);
                RightToolWrist=point+normal*.008f-ToolRotation*(ToolInPalm+tipLocal);
                LeftToolWrist=state.contactPoint-across*.20f+normal*.085f-up.normalized*.06f;
                // Equip movement and the tool share one wrist pose, so the handle cannot float away.
                float enter=Mathf.Min(1,ToolWeight+Time.deltaTime*9);
                Vector3 restR=cameraTransform!=null?cameraTransform.TransformPoint(new Vector3(.25f,-.28f,.44f)):transform.TransformPoint(new Vector3(.60f,.48f,.1f));
                Vector3 restL=cameraTransform!=null?cameraTransform.TransformPoint(new Vector3(-.25f,-.28f,.44f)):transform.TransformPoint(new Vector3(-.60f,.48f,.1f));
                if(Tool!=previousTool)
                {
                    ToolWeight=0;enter=Mathf.Min(1,Time.deltaTime*9);
                    equipRight=previousTool==OpeningTool.None?restR:oldRight;equipLeft=previousTool==OpeningTool.None?restL:oldLeft;
                    equipRotation=previousTool==OpeningTool.None?(cameraTransform!=null?cameraTransform.rotation:transform.rotation):oldRotation;
                }
                RightToolWrist=Vector3.Lerp(equipRight,RightToolWrist,enter);LeftToolWrist=Vector3.Lerp(equipLeft,LeftToolWrist,enter);
                ToolRotation=Quaternion.Slerp(equipRotation,ToolRotation,enter);
                if(item!=null)item.transform.SetPositionAndRotation(RightToolWrist+ToolRotation*ToolInPalm,ToolRotation);
            }
            ToolWeight=Mathf.MoveTowards(ToolWeight,Tool!=OpeningTool.None?1:0,Time.deltaTime*9);
            if(cutter!=null)cutter.SetActive(Tool==OpeningTool.BoxCutter);
            if(prybar!=null)prybar.SetActive(Tool==OpeningTool.PryBar);
            previousTool=Tool;
        }
        public Vector3 SupportWrist(bool left)
        {
            var layout=new CarryLayout(Mathf.Clamp(HeldCount,1,10));
            float x=Mathf.Max(.11f,layout.Width*.32f)*(left?-1:1);
            return CarryAnchor.TransformPoint(new Vector3(x,-.07f,-.07f));
        }
        public void ItemPose(int index,int count,Bounds modelBounds,out Vector3 position,out Quaternion rotation,out float scale)
        {
            var layout=new CarryLayout(count);
            layout.Position(index,out float x,out float y,out float z);
            scale=layout.Scale;
            // Each item rests on the exact same support plane as the palms.
            Vector3 correction=new Vector3(-modelBounds.center.x,-modelBounds.min.y,-modelBounds.center.z)*scale;
            position=CarryAnchor.TransformPoint(new Vector3(x,y,z)+correction);
            rotation=CarryAnchor.rotation;
        }
        void OnDestroy(){if(cutter!=null)Destroy(cutter);if(prybar!=null)Destroy(prybar);}
    }
}
