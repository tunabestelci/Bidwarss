using Bidwarss.Domain;
using UnityEngine;

namespace Bidwarss
{
    // Visual for a single warehouse item. Purely presentational: the server owns all state.
    // Loose items sit a little messy, stacked items snap neat, placements fly in an arc and squash on landing.
    public sealed class ItemVisual : MonoBehaviour
    {
        enum FlightKind : byte { None, Reveal, Place, Drop }

        // Kasa Defteri sınıf renkleri: Çok kötü, Kötü, Orta, İyi, Çok iyi, Destansı, Efsanevi
        public static readonly Color[] ConditionColors={
            new Color32(0x7d,0x85,0x90,255),new Color32(0xa0,0x80,0x5a,255),new Color32(0x4f,0x8f,0xb8,255),
            new Color32(0x3f,0xa1,0x7a,255),new Color32(0x8a,0x63,0xd2,255),new Color32(0xd6,0x47,0x8f,255),new Color32(0xe0,0xa5,0x26,255)};
        // Per condition: grime, rim light, gloss, glow (HDR intensity), outline colour.
        static readonly float[] Wear={.95f,.7f,.45f,.2f,0,0,0};
        static readonly float[] Rim={0,.05f,.1f,.15f,.3f,.45f,.7f};
        static readonly float[] Gloss={0,0,0,.05f,.2f,.4f,.85f};
        static readonly float[] Glow={0,0,0,0,0,.06f,.42f};
        static readonly float[] Mess={9,6,3.5f,2,1,1,1};
        static Mesh tagMesh;

        public int ItemId{get;private set;}
        public int Kind{get;private set;}
        public ItemCondition Condition{get;private set;}
        public int CarryIndex{get;set;}
        public Bounds ModelBounds { get; private set; }
        float pickupStart=-10;Vector3 pickupFrom;

        Collider hitbox;
        Transform art;
        Renderer body,tagPart;
        GameObject shadow;
        ParticleSystem sparkles;
        MaterialPropertyBlock bodyBlock,tagBlock;
        Color baseColor;
        int dollars;
        string title;
        bool customModel;
        ItemLocation previous;
        bool positioned;
        ulong lastHolder=ulong.MaxValue;
        int lastCarryIndex;
        // Real-world presentation: lying on the floor the item is shown at its real size (ItemCatalog.Entry.realSize),
        // in hands and on pallets it shrinks to the compact display size.
        float realFactor=1,sizeNow=-1,normHeight=.38f,serverHalfHeight=.19f,normDepth=.32f,serverHalfDepth=.16f,normWidth=.42f,serverHalfWidth=.21f;
        Vector3 hitSize=new Vector3(.44f,.42f,.34f);
        bool realSized;

        Quaternion pickupRot=Quaternion.identity;float pickupPop;
        FlightKind flight;
        float flightStart,flightDelay,flightDuration,flightArc;
        Vector3 flightFrom;Quaternion flightRotation;
        bool pendingReveal;Vector3 revealFrom;float revealDelay;
        float punchStart=-10,flash,highlight,highlightTarget,appliedHighlight=-1,appliedFlash=-1;
        float messYaw,messRoll;

        public static ItemVisual Create(ItemState state,ItemCatalog.Entry entry,Material material,Material fxMaterial=null)
        {
            var go=new GameObject(entry.title+" #"+state.id);
            var visual=go.AddComponent<ItemVisual>();
            visual.ItemId=state.id;visual.Kind=state.kind;visual.Condition=state.condition;visual.dollars=state.dollars;visual.title=entry.title;
            var box=go.AddComponent<BoxCollider>();box.size=new Vector3(.44f,.42f,.34f);visual.hitbox=box;
            var target=go.AddComponent<InteractionTarget>();target.kind=TargetKind.Item;target.id=state.id;
            var art=new GameObject("Visual").transform;art.SetParent(go.transform,false);visual.art=art;
            // Stable per-item variation so a messy pile looks organic but every client agrees.
            uint h=unchecked((uint)(state.id*73856093)^(uint)(state.kind*19349663));
            h^=h>>13;h*=0x5bd1e995;h^=h>>15;
            float r1=(h&255)/255f-.5f,r2=((h>>8)&255)/255f-.5f,r3=((h>>16)&255)/255f-.5f;
            int condition=(int)state.condition;
            visual.messYaw=r1*2*(8+Mess[condition]);visual.messRoll=r2*2*Mess[condition];
            float tone=1+r3*.12f;
            visual.baseColor=new Color(Mathf.Clamp01(entry.color.r*tone),Mathf.Clamp01(entry.color.g*tone),Mathf.Clamp01(entry.color.b*tone),1);
            if(entry.visualPrefab!=null)
            {
                visual.customModel=true;
                var custom=Instantiate(entry.visualPrefab,art);
                // Only a visual prefab belongs here; networking and interaction live on the parent.
                foreach(var collider in custom.GetComponentsInChildren<Collider>())collider.enabled=false;
                foreach(var body in custom.GetComponentsInChildren<Rigidbody>()){body.isKinematic=true;body.detectCollisions=false;}
                var rs=custom.GetComponentsInChildren<Renderer>();
                if(rs.Length>0)
                {
                    var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
                    Vector3 size=bounds.size;
                    float scale=Mathf.Min(.42f/Mathf.Max(size.x,.001f),.38f/Mathf.Max(size.y,.001f),.32f/Mathf.Max(size.z,.001f));
                    Vector3 center=custom.transform.InverseTransformPoint(bounds.center);
                    custom.transform.localScale*=scale;
                    custom.transform.localPosition-=custom.transform.TransformVector(center);
                    // After the fit the model is centred in the visual: normSize is its size in this space.
                    Vector3 norm=size*scale;
                    var real=entry.realSize;
                    if(real.x>.01f&&real.y>.01f&&real.z>.01f)
                    {
                        // Real size: the catalog size is measured from this very model (Loot > 1 menu), so one factor
                        // (height) gives the exact real size on every axis. A 1.70 m player (eye 1.55 m) is the reference.
                        visual.realFactor=Mathf.Clamp(real.y/Mathf.Max(norm.y,.0005f),.02f,60f);
                        visual.realSized=true;
                        visual.normHeight=norm.y;visual.serverHalfHeight=real.y*.5f;
                        visual.normDepth=norm.z;visual.serverHalfDepth=real.z*.5f;
                        visual.normWidth=norm.x;visual.serverHalfWidth=real.x*.5f;
                    }
                    box.size=new Vector3(norm.x*1.04f,norm.y*1.04f,norm.z*1.04f);box.center=Vector3.zero;
                    visual.hitSize=box.size;
                }
            }
            else
            {
                var bodyObject=new GameObject("Body");bodyObject.transform.SetParent(art,false);
                bodyObject.AddComponent<MeshFilter>().sharedMesh=ItemShapes.Get(entry.sampleShape);
                var renderer=bodyObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                visual.body=renderer;visual.bodyBlock=new MaterialPropertyBlock();
            }
            // Cache only item art bounds before condition tags and effects are added.
            var itemRenderers=art.GetComponentsInChildren<Renderer>();
            var modelBounds=new Bounds(Vector3.zero,new Vector3(.42f,.38f,.32f));
            if(itemRenderers.Length>0){modelBounds=itemRenderers[0].bounds;foreach(var renderer in itemRenderers)modelBounds.Encapsulate(renderer.bounds);}
            visual.ModelBounds=modelBounds;
            // Condition tag: a small coloured hang-tag near the front so value is readable at a glance.
            var tagObject=new GameObject("Condition tag");tagObject.transform.SetParent(art,false);
            if(visual.realSized)
            {
                var bs=box.size;
                tagObject.transform.localPosition=new Vector3(bs.x*.36f,-bs.y*.38f,-bs.z*.54f);
                tagObject.transform.localScale=Vector3.one/Mathf.Sqrt(Mathf.Max(1f,visual.realFactor));
            }
            else tagObject.transform.localPosition=new Vector3(.155f,-.15f,-.175f);
            tagObject.transform.localRotation=Quaternion.Euler(0,0,-8);
            tagObject.AddComponent<MeshFilter>().sharedMesh=TagMesh();
            var tagRenderer=tagObject.AddComponent<MeshRenderer>();tagRenderer.sharedMaterial=material;
            tagRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;tagRenderer.receiveShadows=false;
            visual.tagPart=tagRenderer;visual.tagBlock=new MaterialPropertyBlock();
            if(fxMaterial!=null)
            {
                visual.shadow=new GameObject("Contact shadow");visual.shadow.transform.SetParent(go.transform,false);
                visual.shadow.transform.localPosition=new Vector3(0,-(visual.realSized?visual.normHeight*.5f:.19f)-.012f,0);
                visual.shadow.transform.localScale=visual.realSized?new Vector3(Mathf.Max(.62f,box.size.x*1.5f),1,Mathf.Max(.5f,box.size.z*1.5f)):new Vector3(.62f,1,.5f);
                visual.shadow.AddComponent<MeshFilter>().sharedMesh=ProceduralMeshes.ShadowQuad();
                var shadowRenderer=visual.shadow.AddComponent<MeshRenderer>();shadowRenderer.sharedMaterial=fxMaterial;
                shadowRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;shadowRenderer.receiveShadows=false;
                if(state.condition==ItemCondition.Legendary||state.condition==ItemCondition.VeryGood)
                    visual.sparkles=CreateSparkles(art,fxMaterial,state.condition==ItemCondition.Legendary);
            }
            visual.Apply(true);
            return visual;
        }

        static Mesh TagMesh()
        {
            if(tagMesh!=null)return tagMesh;
            var b=new MeshBuilder();
            b.AddRoundedBox(new Vector3(0,0,0),new Vector3(.075f,.1f,.016f),.01f,Color.white,true);
            b.AddRoundedBox(new Vector3(0,.038f,-.009f),new Vector3(.06f,.018f,.006f),.003f,new Color(.96f,.94f,.88f),false);
            b.AddSphere(new Vector3(0,.06f,0),.011f,new Color(.75f,.77f,.8f),false);
            tagMesh=b.ToMesh("Bidwarss condition tag");
            return tagMesh;
        }

        static ParticleSystem CreateSparkles(Transform parent,Material fx,bool legendary)
        {
            var go=new GameObject("Sparkles");go.transform.SetParent(parent,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=true;main.duration=2;main.startLifetime=new ParticleSystem.MinMaxCurve(.7f,1.4f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.02f,.12f);main.startSize=new ParticleSystem.MinMaxCurve(.03f,legendary?.09f:.06f);
            main.startColor=legendary?new Color(1,.86f,.38f,.95f):new Color(.55f,.85f,1,.85f);
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=legendary?24:10;main.gravityModifier=-.03f;
            var emission=ps.emission;emission.rateOverTime=legendary?9:3;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(.38f,.34f,.28f);
            var size=ps.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,0),new Keyframe(.35f,1),new Keyframe(1,0)));
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=fx;
            ps.Play();
            return ps;
        }

        // First appearance out of a crate: fly from the crate mouth into place.
        public void BeginReveal(Vector3 from,float delay){pendingReveal=true;revealFrom=from;revealDelay=delay;}
        public void SetHighlight(float value){highlightTarget=value;}
        bool LocalHolds(ulong holder){var local=WarehousePlayer.Local;return local!=null&&local.OwnerClientId==holder;}

        void SetVisible(bool visible)
        {
            if(art!=null&&art.gameObject.activeSelf!=visible)art.gameObject.SetActive(visible);
            if(shadow!=null&&!visible)shadow.SetActive(false);
        }

        void Begin(FlightKind kind,Vector3 from,Quaternion rotation,float delay,float duration,float arc)
        {
            flight=kind;flightFrom=from;flightRotation=rotation;flightStart=Time.time;flightDelay=delay;flightDuration=duration;flightArc=arc;
        }

        public void UpdateState(ItemState state,Vector3 position,Quaternion rotation,float scale=1)
        {
            bool held=state.location==ItemLocation.Held;
            // Size: real while loose on the floor, compact when carried or stacked (smooth transition).
            float sizeTarget=state.location==ItemLocation.Loose?realFactor:1f;
            if(held)sizeNow=1f;
            sizeNow=sizeNow<0?sizeTarget:Mathf.Lerp(sizeNow,sizeTarget,1-Mathf.Exp(-12*Time.deltaTime));
            transform.localScale=Vector3.one*scale*sizeNow;
            // tiny things (coins, notes) still get a hand-sized target (12 cm) so they can be picked up
            if(hitbox is BoxCollider bc)
            {
                float min=.12f/Mathf.Max(.0001f,scale*sizeNow);
                bc.size=new Vector3(Mathf.Max(hitSize.x,min),Mathf.Max(hitSize.y,min),Mathf.Max(hitSize.z,min));
            }
            // The server places a loose item at floor + realSize.y/2; this puts the visible bottom exactly on the floor.
            // Lying on its back inside a crate (pose 2) the item's depth is its height.
            if(state.location==ItemLocation.Loose&&realSized)
                position.y+=state.pose==2?normDepth*sizeNow*.5f-serverHalfDepth:state.pose==3?normWidth*sizeNow*.5f-serverHalfWidth:normHeight*sizeNow*.5f-serverHalfHeight;
            if(!positioned)
            {
                positioned=true;previous=state.location;if(held)lastHolder=state.holder;
                if(pendingReveal)
                {
                    pendingReveal=false;transform.SetPositionAndRotation(revealFrom,rotation);
                    Begin(FlightKind.Reveal,revealFrom,rotation,revealDelay,.62f,.85f);
                }
                else transform.SetPositionAndRotation(position,rotation);
            }
            else if(previous!=state.location)
            {
                var from=transform.position;
                if(previous==ItemLocation.Held&&state.location==ItemLocation.Stacked)
                    Begin(FlightKind.Place,from,transform.rotation,lastCarryIndex*.045f,.3f,.28f);
                else if(previous==ItemLocation.Held&&state.location==ItemLocation.Loose)
                    Begin(FlightKind.Drop,from,transform.rotation,0,.22f,.16f);
                if(state.location==ItemLocation.Held){pickupStart=Time.time;pickupFrom=from;pickupRot=transform.rotation;flight=FlightKind.None;FeedbackHub.PickedUp(from,LocalHolds(state.holder));}
                previous=state.location;
            }
            if(held){lastHolder=state.holder;lastCarryIndex=CarryIndex;}
            highlight=Mathf.MoveTowards(highlight,highlightTarget,Time.deltaTime*5f);
            if(flash>0)flash=Mathf.Max(0,flash-Time.deltaTime*1.8f);
            if(Mathf.Abs(highlight-appliedHighlight)>.01f||Mathf.Abs(flash-appliedFlash)>.01f)Apply(false);

            Quaternion neat=Quaternion.identity;
            // Messy pose only on the floor; inside a crate the item stands neatly so it does not poke through the walls.
            Quaternion mess=state.location==ItemLocation.Loose&&state.pose==0?Quaternion.Euler(0,messYaw,messRoll):Quaternion.identity;
            if(flight!=FlightKind.None)
            {
                float t=(Time.time-flightStart-flightDelay)/flightDuration;
                if(t<0)
                {
                    transform.SetPositionAndRotation(flightFrom,flightRotation);
                    SetVisible(flight!=FlightKind.Reveal);
                }
                else if(t>=1)
                {
                    var kind=flight;flight=FlightKind.None;SetVisible(true);
                    transform.SetPositionAndRotation(position,rotation);
                    Land(kind,position);
                }
                else
                {
                    SetVisible(true);
                    float ease=1-(1-t)*(1-t)*(1-t);
                    transform.position=Vector3.Lerp(flightFrom,position,ease)+Vector3.up*(Mathf.Sin(Mathf.PI*t)*flightArc);
                    transform.rotation=Quaternion.Slerp(flightRotation,rotation,ease);
                    if(flight==FlightKind.Reveal)art.localScale=Vector3.one*Mathf.Lerp(.3f,1,ease);
                }
            }
            else if(held)
            {
                // Lifted out of the crate / off the floor in a short arc, tilting into the carry pose, with a small pop on arrival.
                float pt=Mathf.Clamp01((Time.time-pickupStart)/.3f);
                float pe=1-Mathf.Pow(1-pt,3);
                float lift=Mathf.Sin(Mathf.PI*pt)*.14f*(1-pt*.3f);
                transform.SetPositionAndRotation(Vector3.Lerp(pickupFrom,position,pe)+Vector3.up*lift,Quaternion.Slerp(pickupRot,rotation,pe));
                pickupPop=pt<1?Mathf.Sin(Mathf.PI*pt)*.07f:0;
            }
            else transform.SetPositionAndRotation(position,rotation);

            if(held){art.localScale=Vector3.one*(1+pickupPop);art.localRotation=Quaternion.identity;}
            else if(flight==FlightKind.None)
            {
                // Squash-and-stretch spring after landing, then the messy/neat resting pose.
                float since=Time.time-punchStart;
                float spring=since<.7f?Mathf.Exp(-9*since)*Mathf.Cos(since*22):0;
                art.localScale=new Vector3(1+.12f*spring,1-.2f*spring,1+.12f*spring);
                art.localRotation=Quaternion.Slerp(art.localRotation,mess,1-Mathf.Exp(-10*Time.deltaTime));
            }
            else art.localRotation=Quaternion.Slerp(art.localRotation,neat,1-Mathf.Exp(-14*Time.deltaTime));
            // Items are only targetable once they have landed (also keeps server-side look checks off invisible items).
            hitbox.enabled=!held&&flight==FlightKind.None;
            if(shadow!=null)shadow.SetActive(state.location==ItemLocation.Loose&&state.pose==0&&flight==FlightKind.None);
            if(sparkles!=null)
            {
                var emission=sparkles.emission;bool emit=state.location!=ItemLocation.Sealed&&(flight==FlightKind.None||flight==FlightKind.Place);
                if(emission.enabled!=emit)emission.enabled=emit;
            }
        }

        void Land(FlightKind kind,Vector3 position)
        {
            punchStart=Time.time;
            if(kind==FlightKind.Place)FeedbackHub.Placed(position,dollars,LocalHolds(lastHolder));
            else if(kind==FlightKind.Drop)FeedbackHub.Dropped(position,LocalHolds(lastHolder));
            else if(kind==FlightKind.Reveal)
            {
                if(Condition>=ItemCondition.Good)flash=1;
                FeedbackHub.Revealed(position,Condition,title,dollars);
            }
        }

        void Apply(bool force)
        {
            appliedHighlight=highlight;appliedFlash=flash;
            int c=(int)Condition;
            if(body!=null&&bodyBlock!=null)
            {
                bodyBlock.SetColor("_BaseColor",baseColor);
                bodyBlock.SetFloat("_VertexColor",1);
                bodyBlock.SetFloat("_Wear",Wear[c]);
                bodyBlock.SetFloat("_RimStrength",Rim[c]);
                bodyBlock.SetFloat("_Specular",Gloss[c]);
                bodyBlock.SetFloat("_OutlineWidth",.0034f);
                bodyBlock.SetColor("_OutlineColor",Condition==ItemCondition.Legendary?new Color(.55f,.36f,.05f):new Color(.06f,.06f,.1f));
                var glow=Condition==ItemCondition.Legendary?new Color(1f,.72f,.2f):new Color(.3f,.6f,1f);
                Color emission=glow*Glow[c]+new Color(1,.85f,.4f)*(flash*.9f);
                bodyBlock.SetColor("_Emission",emission);
                bodyBlock.SetFloat("_Highlight",highlight);
                body.SetPropertyBlock(bodyBlock);
            }
            if(tagPart!=null&&tagBlock!=null)
            {
                tagBlock.SetColor("_BaseColor",ConditionColors[c]);
                tagBlock.SetFloat("_VertexColor",1);
                tagBlock.SetFloat("_OutlineWidth",.0022f);
                tagBlock.SetColor("_OutlineColor",new Color(.05f,.06f,.09f));
                tagBlock.SetFloat("_RimStrength",.2f);
                tagBlock.SetFloat("_Highlight",highlight*.6f);
                tagPart.SetPropertyBlock(tagBlock);
            }
        }
    }
}
