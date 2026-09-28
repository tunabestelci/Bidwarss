using UnityEngine;

namespace Bidwarss
{
    public static class RevealEffects
    {
        static AudioClip revealClip,winClip;
        public static void Play(Vector3 position,Material material)
        {
            if(Application.isBatchMode)return;
            var go=new GameObject("Kutu tozu");go.transform.position=position;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.duration=.35f;main.startLifetime=.75f;main.startSpeed=1.5f;
            main.startSize=new ParticleSystem.MinMaxCurve(.25f,.7f);main.startColor=new Color(.85f,.72f,.5f,.65f);
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=64;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,48)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.65f;
            var color=ps.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.2f),new Keyframe(1,1.7f)));
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;ps.Play();Object.Destroy(go,2);
            if(revealClip==null)revealClip=Tone("Kutu açıldı",.18f,160,40);
            AudioSource.PlayClipAtPoint(revealClip,position,.25f);
        }
        public static void Celebrate()
        {
            if(Application.isBatchMode)return;
            if(winClip==null)winClip=Tone("Depo tamamlandı",.65f,440,880);
            var player=WarehousePlayer.Local;
            if(player!=null)AudioSource.PlayClipAtPoint(winClip,player.transform.position+Vector3.up*1.5f,.18f);
        }
        static AudioClip Tone(string name,float duration,float from,float to)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(duration*rate)];float phase=0;
            for(int i=0;i<samples.Length;i++){float t=(float)i/samples.Length;phase+=2*Mathf.PI*Mathf.Lerp(from,to,t)/rate;samples[i]=Mathf.Sin(phase)*Mathf.Sin(t*Mathf.PI)*(1-t)*.6f;}
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
    }
}
