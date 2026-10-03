using UnityEngine;

namespace Bidwarss
{
    // Analytic two-bone solver; no package or Humanoid retargeting dependency.
    public static class BrunoArmIK
    {
        public static void Solve(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole,Quaternion rotation,float weight)
        {
            if(upper==null || lower==null || hand==null || weight<=0)return;
            Vector3 shoulder=upper.position;
            float a=Vector3.Distance(shoulder,lower.position),b=Vector3.Distance(lower.position,hand.position);
            Vector3 delta=target-shoulder;float distance=delta.magnitude;
            if(a<.0001f || b<.0001f || distance<.0001f)return;
            Vector3 direction=delta/distance;
            distance=Mathf.Clamp(distance,Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 bend=Vector3.ProjectOnPlane(pole-shoulder,direction);
            if(bend.sqrMagnitude<.00001f)bend=Vector3.Cross(direction,Vector3.up);
            if(bend.sqrMagnitude<.00001f)bend=Vector3.Cross(direction,Vector3.right);
            float along=(a*a-b*b+distance*distance)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 elbow=shoulder+direction*along+bend.normalized*height;
            Quaternion upperTarget=Quaternion.FromToRotation(lower.position-shoulder,elbow-shoulder)*upper.rotation;
            upper.rotation=Quaternion.Slerp(upper.rotation,upperTarget,weight);
            Vector3 reachable=shoulder+direction*distance;
            Quaternion lowerTarget=Quaternion.FromToRotation(hand.position-lower.position,reachable-lower.position)*lower.rotation;
            lower.rotation=Quaternion.Slerp(lower.rotation,lowerTarget,weight);
            hand.rotation=Quaternion.Slerp(hand.rotation,rotation,weight);
        }
    }
}
