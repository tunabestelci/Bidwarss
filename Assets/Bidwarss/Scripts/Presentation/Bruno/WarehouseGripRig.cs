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
        public bool IsWorking { get; private set; }
        public float ToolPhase { get; private set; }
        public Vector3 LeftToolWrist { get; private set; }
        public Vector3 RightToolWrist { get; private set; }
        public Quaternion ToolRotation { get; private set; }
        public float ToolWeight { get; private set; }
        WarehousePlayer owner;
        Transform cameraTransform;
        GameObject cutter, prybar;
        Transform cutterTip, pryTipNode;
        OpeningTool previousTool, shown = OpeningTool.PryBar;
        int lastStep = -1, lastBox = -1;
        float popTime = -10f;
        static readonly Vector3 ToolInPalm = new Vector3(0,.055f,.04f);
        // Pry bar: working end measured from the model itself (not from the ToolTip node, whose position depends on the FBX
        // units). pryFix turns the model so that its working end (the claw, the end farthest from the grip) points along +Z,
        // pryTip is that end in the rig's frame (metres).
        Quaternion pryFix = Quaternion.identity;
        Vector3 pryTip;
        bool pryMeasured;
        public void Initialize(WarehousePlayer player)
        {
            owner = player;
            CarryAnchor = new GameObject("Carry support plane").transform;
            CarryAnchor.SetParent(transform,false);
            CarryAnchor.localPosition = new Vector3(0,1.04f,.54f);
            cutter = CreateCutter(out cutterTip);
            prybar = CreateTool("Bruno/PryBarTool", out pryTipNode);
            if(prybar!=null)pryMeasured=MeasureToolEnd(prybar,out pryTip,out pryFix);
        }
        // The longest axis of the tool is its shaft; the pivot sits at the grip, so the end farther from the pivot is the
        // working end. Returns the rotation that points that end along +Z and the end's position after that rotation.
        static bool MeasureToolEnd(GameObject tool,out Vector3 tipRig,out Quaternion fix)
        {
            tipRig=new Vector3(0,0,.3f);fix=Quaternion.identity;
            var root=tool.transform;var inverse=Quaternion.Inverse(root.rotation);
            bool any=false;Vector3 lo=Vector3.zero,hi=Vector3.zero;
            foreach(var renderer in tool.GetComponentsInChildren<Renderer>(true))
            {
                if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;
                Bounds local;
                if(renderer is SkinnedMeshRenderer skinned){if(skinned.sharedMesh==null)continue;local=skinned.sharedMesh.bounds;}
                else{var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;local=filter.sharedMesh.bounds;}
                var matrix=renderer.transform.localToWorldMatrix;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 p=inverse*(matrix.MultiplyPoint3x4(corner)-root.position);
                    if(!any){lo=hi=p;any=true;}
                    else{lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}
                }
            }
            if(!any)return false;
            Vector3 size=hi-lo;
            int axis=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2;
            float end=Mathf.Abs(hi[axis])>=Mathf.Abs(lo[axis])?hi[axis]:lo[axis];
            Vector3 direction=Vector3.zero;direction[axis]=Mathf.Sign(end);
            Vector3 tip=(lo+hi)*.5f;tip[axis]=end-Mathf.Sign(end)*.006f;
            fix=Vector3.Dot(direction,Vector3.forward)<-.99f?Quaternion.AngleAxis(180,Vector3.up):Quaternion.FromToRotation(direction,Vector3.forward);
            tipRig=fix*tip;
            return true;
        }
        Transform blade;
        float bladeOffset=-.034f;
        GameObject CreateCutter(out Transform tip)
        {
            var holder=new GameObject("Bruno box cutter");holder.transform.SetParent(transform,false);
            var material=Resources.Load<Material>("BoxCutter/BoxCutter_Cel");
            if(material==null)
            {
                var shader=Shader.Find("Bidwars/CelLit");
                if(shader==null)shader=Shader.Find("Bidwarss/BrunoCel");
                if(shader!=null){material=new Material(shader);var tex=Resources.Load<Texture2D>("BoxCutter/BoxCutter_BaseColor");if(tex!=null)material.SetTexture("_BaseMap",tex);}
            }
            for(int i=0;i<2;i++)
            {
                var part=new GameObject(i==0?"Body":"Blade");part.transform.SetParent(holder.transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=i==0?BoxCutterMesh.Body:BoxCutterMesh.Blade;
                part.AddComponent<MeshRenderer>().sharedMaterial=material;
                if(i==1)blade=part.transform;
            }
            tip=new GameObject("ToolTip").transform;tip.SetParent(holder.transform,false);tip.localPosition=new Vector3(0,-.0074f,.1224f);
            BrunoOutline.Cap(holder,BrunoOutline.ToolWidth);
            holder.SetActive(false);return holder;
        }
        GameObject CreateTool(string path,out Transform tip)
        {
            tip=null;
            var prefab=Resources.Load<GameObject>(path);
            if(prefab==null){Debug.LogWarning("Bruno tool missing: "+path+". Run Build Character Assets.",this);return null;}
            var instance=Instantiate(prefab,transform,false);
            foreach(var t in instance.GetComponentsInChildren<Transform>()) if(t.name=="ToolTip")tip=t;
            BrunoOutline.Cap(instance,BrunoOutline.ToolWidth);
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
            int workingBox=-1;
            for(int i=0;i<world.Crates.Count;i++)
                if(!world.Crates[i].opened && world.Crates[i].opener==owner.OwnerClientId){workingBox=i;break;}
            int selected=workingBox>=0?world.ToolFor(workingBox):owner.Tool;
            Tool=HeldCount>0?OpeningTool.None:selected==WarehousePlayer.CutterSlot?OpeningTool.BoxCutter:selected==WarehousePlayer.OpenerSlot?OpeningTool.PryBar:OpeningTool.None;
            IsWorking=workingBox>=0 && Tool!=OpeningTool.None;
            ToolPhase=IsWorking?world.Crates[workingBox].progress:0;
            var frame=cameraTransform!=null?cameraTransform:transform;
            Vector3 restR=cameraTransform!=null?frame.TransformPoint(new Vector3(.25f,-.28f,.44f)):frame.TransformPoint(new Vector3(.38f,1.05f,.4f));
            Vector3 restL=cameraTransform!=null?frame.TransformPoint(new Vector3(-.25f,-.28f,.44f)):frame.TransformPoint(new Vector3(-.38f,1.05f,.4f));
            Quaternion desiredRotation=frame.rotation;
            Vector3 desiredRight=restR,desiredLeft=restL;
            if(Tool!=OpeningTool.None)shown=Tool;
            var item=shown==OpeningTool.BoxCutter?cutter:prybar;
            var tip=shown==OpeningTool.BoxCutter?cutterTip:pryTipNode;
            if(shown==OpeningTool.BoxCutter)
            {
                // Held like a knife: the handle in the fist, the blade pointing up and slightly forward.
                desiredRotation=frame.rotation*Quaternion.Euler(-70f,8f,-6f);
                if(cameraTransform!=null)desiredRight=frame.TransformPoint(new Vector3(.23f,-.31f,.42f));
            }
            if(IsWorking && world.WorkPoint(workingBox,out var point,out var along,out var normal))
            {
                if(normal.sqrMagnitude<.001f)normal=-transform.forward;
                normal.Normalize();
                if(along.sqrMagnitude<.001f)along=Vector3.ProjectOnPlane(transform.right,normal);
                along.Normalize();
                float cutT=Time.time*11f;
                float stroke=Mathf.Sin(cutT);
                Vector3 forward=Tool==OpeningTool.BoxCutter?(along*.75f-normal*.65f).normalized:-normal;
                Vector3 up=Vector3.ProjectOnPlane(normal+along*.3f,forward);
                if(up.sqrMagnitude<.001f)up=Vector3.ProjectOnPlane(Vector3.up,forward);
                if(up.sqrMagnitude<.001f)up=Vector3.ProjectOnPlane(transform.forward,forward);
                desiredRotation=Quaternion.LookRotation(forward,up.normalized);
                float plunge=0,kick=0;
                if(Tool==OpeningTool.PryBar)
                {
                    // One screw = one stroke: the lever is pushed down slowly (ratchet), snaps back, and kicks when the screw lets go.
                    int steps=world.WorkSteps(workingBox);
                    float cycle=ToolPhase*steps;
                    int step=Mathf.Min(steps-1,Mathf.FloorToInt(cycle));
                    float u=cycle-step;
                    if(step!=lastStep||workingBox!=lastBox){if(lastStep>=0&&lastBox==workingBox&&step>lastStep)popTime=Time.time;lastStep=step;lastBox=workingBox;}
                    float ratchet=u<.8f?Mathf.SmoothStep(0,1,u/.8f):1-(u-.8f)/.2f;
                    kick=Mathf.Exp(-(Time.time-popTime)*11f);
                    desiredRotation*=Quaternion.Euler(-ratchet*26f+Mathf.Sin(Time.time*41f)*1.3f*ratchet+kick*9f,0,Mathf.Sin(Time.time*7f)*2.5f*ratchet);
                }
                else
                {
                    // box cutter: plunges in at the start, then saws along the tape with a little roll and pressure
                    plunge=Mathf.Clamp01(1-ToolPhase*25f);
                    desiredRotation*=Quaternion.Euler(0,0,Mathf.Cos(cutT)*4.5f)*Quaternion.Euler(Mathf.Sin(cutT*.5f)*2f-plunge*18f,0,0);
                }
                Vector3 tipLocal=shown==OpeningTool.PryBar&&pryMeasured?pryTip:item!=null && tip!=null?item.transform.InverseTransformPoint(tip.position):new Vector3(0,0,.2f);
                Vector3 contact=point+normal*(.004f+kick*.035f-plunge*.012f+(Tool==OpeningTool.BoxCutter?Mathf.Sin(cutT*2f)*.0015f:0f))+(Tool==OpeningTool.BoxCutter?along*(stroke*(.014f+.006f*Mathf.Sin(ToolPhase*30f))):Vector3.zero);
                desiredRight=contact-desiredRotation*(ToolInPalm+tipLocal);
                desiredLeft=point-along*.18f+normal*.09f;
            }
            if(!IsWorking){lastStep=-1;lastBox=-1;}
            // Draw / put away: the tool comes up from below-right when it is selected and sinks away when it is not.
            float away=1-Mathf.SmoothStep(0,1,ToolWeight);
            Vector3 awayOffset=frame.TransformDirection(new Vector3(.09f,-.26f,-.10f));
            if(away>.001f)
            {
                desiredRight+=awayOffset*away;desiredLeft+=awayOffset*.6f*away;
                desiredRotation=Quaternion.Slerp(desiredRotation,desiredRotation*Quaternion.Euler(45f,0,-20f),away);
            }
            else if(!IsWorking&&Tool!=OpeningTool.None)
            {
                // held ready: slow breathing sway
                float s=Mathf.Sin(Time.time*1.6f);
                desiredRight+=frame.TransformDirection(new Vector3(0,s*.004f,0));
                desiredRotation*=Quaternion.Euler(s*2.5f,0,Mathf.Sin(Time.time*1.1f)*2f);
            }
            if(Tool!=previousTool&&Tool!=OpeningTool.None){RightToolWrist=desiredRight;LeftToolWrist=desiredLeft;ToolRotation=desiredRotation;ToolWeight=0;}
            float blend=1-Mathf.Exp(-18*Time.deltaTime);
            RightToolWrist=Vector3.Lerp(RightToolWrist,desiredRight,blend);
            LeftToolWrist=Vector3.Lerp(LeftToolWrist,desiredLeft,blend);
            ToolRotation=Quaternion.Slerp(ToolRotation,desiredRotation,blend);
            if(item!=null)item.transform.SetPositionAndRotation(RightToolWrist+ToolRotation*ToolInPalm,item==prybar&&pryMeasured?ToolRotation*pryFix:ToolRotation);
            ToolWeight=Mathf.MoveTowards(ToolWeight,Tool!=OpeningTool.None?1:0,Time.deltaTime*5f);
            bladeOffset=Mathf.MoveTowards(bladeOffset,Tool==OpeningTool.BoxCutter&&ToolWeight>.7f?0:-.034f,Time.deltaTime*.16f);
            if(blade!=null)blade.localPosition=new Vector3(0,0,Mathf.Round(bladeOffset/.0085f)*.0085f);
            bool visible=Tool!=OpeningTool.None||ToolWeight>.03f;
            if(cutter!=null)cutter.SetActive(visible&&shown==OpeningTool.BoxCutter);
            if(prybar!=null)prybar.SetActive(visible&&shown==OpeningTool.PryBar);
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
