using UnityEngine;

namespace Bidwarss
{
    public sealed class PalletVisual : MonoBehaviour
    {
        void Start()
        {
            var original=GetComponent<MeshRenderer>();
            if(original==null)return;
            var material=original.sharedMaterial;original.enabled=false;
            var root=new GameObject("Pallet planks").transform;
            root.SetPositionAndRotation(transform.position,transform.rotation);
            root.SetParent(transform,true); // Cancel primitive scale; dimensions below are metres.
            for(int i=0;i<5;i++)Part(root,new Vector3(-.56f+i*.28f,.08f,0),new Vector3(.23f,.09f,2.3f),material);
            for(int i=-1;i<=1;i++)Part(root,new Vector3(i*.53f,-.015f,0),new Vector3(.17f,.1f,2.15f),material);
        }
        static void Part(Transform root,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Wood plank";go.transform.SetParent(root,false);
            go.transform.localPosition=position;go.transform.localScale=size;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
