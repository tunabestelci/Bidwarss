using UnityEngine;
namespace Bidwarss
{
    public sealed class StackLayout : MonoBehaviour
    {
        public Transform[] cells;
        public Vector3 Center(int index)=>cells[index].position;
        public Quaternion Rotation(int index)=>cells[index].rotation;
        void OnDrawGizmosSelected()
        {
            if(cells==null)return;Gizmos.color=Color.cyan;
            foreach(var cell in cells)if(cell!=null){Gizmos.matrix=Matrix4x4.TRS(cell.position,cell.rotation,Vector3.one);Gizmos.DrawWireCube(Vector3.zero,new Vector3(.44f,.42f,.34f));}
        }
    }
}
