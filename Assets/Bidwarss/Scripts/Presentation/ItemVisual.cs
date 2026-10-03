using Bidwarss.Domain;
using UnityEngine;

namespace Bidwarss
{
    public sealed class ItemVisual : MonoBehaviour
    {
        Collider hitbox;
        Renderer[] renderers;
        ItemLocation previous;
        bool positioned;
        public Bounds ModelBounds { get; private set; }
        Vector3 pickupOrigin;
        float pickupTime=-1;
        public static readonly Color[] ConditionColors={
            new Color(.44f,.35f,.3f),new Color(.63f,.38f,.27f),new Color(.85f,.57f,.22f),
            new Color(.85f,.87f,.9f),new Color(.32f,.85f,.5f),new Color(.3f,.65f,1),new Color(1,.76f,.18f)};
        public static ItemVisual Create(ItemState state,ItemCatalog.Entry entry,Material material)
        {
            var go=new GameObject(entry.title+" #"+state.id);
            var visual=go.AddComponent<ItemVisual>();
            var box=go.AddComponent<BoxCollider>();box.size=new Vector3(.44f,.42f,.34f);visual.hitbox=box;
            var target=go.AddComponent<InteractionTarget>();target.kind=TargetKind.Item;target.id=state.id;
            var art=new GameObject("Visual").transform;art.SetParent(go.transform,false);
            if(entry.visualPrefab!=null)
            {
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
                }
            }
            else BuildSample(art,entry.sampleShape,entry.color,material);
            // The colored seal communicates condition without recoloring the user's artwork.
            Part(art,"Condition seal",new Vector3(.16f,-.12f,-.185f),new Vector3(.07f,.08f,.025f),ConditionColors[(int)state.condition],material);
            visual.renderers=go.GetComponentsInChildren<Renderer>();
            var combined=new Bounds(Vector3.zero,Vector3.zero);bool first=true;
            foreach(var renderer in visual.renderers)
            {
                if(first){combined=renderer.bounds;first=false;}else combined.Encapsulate(renderer.bounds);
            }
            visual.ModelBounds=combined;
            return visual;
        }
        static void BuildSample(Transform root,ItemCatalog.SampleShape shape,Color color,Material mat)
        {
            Color dark=new Color(.10f,.13f,.18f);
            switch(shape)
            {
                case ItemCatalog.SampleShape.Mirror:
                    Part(root,"Frame",Vector3.zero,new Vector3(.35f,.38f,.07f),color,mat);
                    Part(root,"Glass",new Vector3(0,0,-.045f),new Vector3(.27f,.30f,.025f),new Color(.5f,.87f,.94f),mat);break;
                case ItemCatalog.SampleShape.Table:
                    Part(root,"Top",new Vector3(0,.12f,0),new Vector3(.42f,.065f,.3f),color,mat);
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Part(root,"Leg",new Vector3(x*.16f,-.045f,z*.1f),new Vector3(.045f,.27f,.045f),dark,mat);break;
                case ItemCatalog.SampleShape.Chair:
                    Part(root,"Seat",new Vector3(0,-.01f,0),new Vector3(.29f,.055f,.27f),color,mat);
                    Part(root,"Back",new Vector3(0,.10f,.10f),new Vector3(.29f,.22f,.05f),color,mat);
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Part(root,"Leg",new Vector3(x*.11f,-.1f,z*.09f),new Vector3(.04f,.2f,.04f),dark,mat);break;
                case ItemCatalog.SampleShape.Radio:
                    Part(root,"Body",Vector3.zero,new Vector3(.4f,.27f,.24f),color,mat);
                    Part(root,"Speaker",new Vector3(-.085f,0,-.135f),new Vector3(.15f,.18f,.02f),dark,mat);
                    Part(root,"Dial",new Vector3(.11f,.02f,-.14f),new Vector3(.06f,.06f,.03f),Color.white,mat);break;
                case ItemCatalog.SampleShape.Lamp:
                    Part(root,"Base",new Vector3(0,-.16f,0),new Vector3(.25f,.045f,.25f),dark,mat);
                    Part(root,"Stem",new Vector3(0,-.04f,0),new Vector3(.035f,.24f,.035f),dark,mat);
                    Part(root,"Shade",new Vector3(0,.12f,0),new Vector3(.3f,.15f,.25f),color,mat);break;
                default:Part(root,"Sample",Vector3.zero,new Vector3(.34f,.32f,.28f),color,mat);break;
            }
        }
        static void Part(Transform parent,string name,Vector3 pos,Vector3 size,Color color,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=size;
            var col=go.GetComponent<Collider>();col.enabled=false;Destroy(col);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=mat;
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);
        }
        public void UpdateState(ItemState state,Vector3 position,Quaternion rotation,float scale=1)
        {
            transform.localScale=Vector3.one*scale;
            bool held=state.location==ItemLocation.Held;
            hitbox.enabled=!held;
            if(held && positioned && previous!=ItemLocation.Held){pickupOrigin=transform.position;pickupTime=Time.time;}
            bool arriving=held && positioned && Time.time-pickupTime<.14f;
            float t=Mathf.SmoothStep(0,1,(Time.time-pickupTime)/.14f);
            transform.SetPositionAndRotation(arriving?Vector3.Lerp(pickupOrigin,position,t):position,rotation);
            positioned=true;previous=state.location;
        }
    }
}
