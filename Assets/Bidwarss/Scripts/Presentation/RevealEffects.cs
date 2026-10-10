using Bidwarss.Domain;
using UnityEngine;

namespace Bidwarss
{
    // All audio is synthesised at runtime so the prototype ships without audio assets.
    // Replace the clips with authored sounds later; call sites stay the same.
    public static class RevealEffects
    {
        static AudioClip revealClip,winClip,pickClip,placeClip,dropClip,stepClip,ambientClip,creakClip,landClip,popClip,gripClip;
        static float lastItemSound,lastStep,lastLand,lastPop,lastGrip;
        // Container opened: a dust cloud rolling out of the doors, flying splinters and a groaning hinge.
        public static void Play(Vector3 position,Material material)
        {
            if(Application.isBatchMode)return;
            var go=new GameObject("Kutu tozu");go.transform.position=position;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.duration=.5f;main.startLifetime=new ParticleSystem.MinMaxCurve(.9f,1.5f);main.startSpeed=new ParticleSystem.MinMaxCurve(.8f,2.4f);
            main.startSize=new ParticleSystem.MinMaxCurve(.35f,.95f);main.startColor=new Color(.85f,.72f,.5f,.6f);
            main.gravityModifier=-.04f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=96;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,40),new ParticleSystem.Burst(.18f,28)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.8f;
            var color=ps.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.2f),new Keyframe(1,1.9f)));
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;ps.Play();Object.Destroy(go,3);
            // Short, fast, dark flecks that fall back down.
            var chips=new GameObject("Kutu kıymıkları");chips.transform.position=position;
            var cs=chips.AddComponent<ParticleSystem>();cs.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var cm=cs.main;cm.loop=false;cm.duration=.2f;cm.startLifetime=new ParticleSystem.MinMaxCurve(.5f,.9f);cm.startSpeed=new ParticleSystem.MinMaxCurve(2.5f,5f);
            cm.startSize=new ParticleSystem.MinMaxCurve(.04f,.1f);cm.startColor=new Color(.35f,.25f,.16f,1);cm.gravityModifier=1.6f;
            cm.simulationSpace=ParticleSystemSimulationSpace.World;cm.maxParticles=24;
            var ce=cs.emission;ce.rateOverTime=0;ce.SetBursts(new[]{new ParticleSystem.Burst(0,18)});
            var csh=cs.shape;csh.shapeType=ParticleSystemShapeType.Cone;csh.angle=38;csh.radius=.3f;
            cs.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;cs.Play();Object.Destroy(chips,2);
            if(revealClip==null)revealClip=Tone("Kutu açıldı",.18f,160,40);
            AudioSource.PlayClipAtPoint(revealClip,position,.28f);
            if(creakClip==null)creakClip=Creak();
            AudioSource.PlayClipAtPoint(creakClip,position,.32f);
        }
        // A piece leaving the container.
        public static void Pop(Vector3 position)
        {
            if(Application.isBatchMode||Time.unscaledTime-lastPop<.045f)return;
            lastPop=Time.unscaledTime;
            if(popClip==null)popClip=Tone("Eşya çıktı",.09f,280,620);
            AudioSource.PlayClipAtPoint(popClip,position,.1f);
        }
        // A piece settling on the floor.
        public static void Land(Vector3 position)
        {
            if(Application.isBatchMode||Time.unscaledTime-lastLand<.04f)return;
            lastLand=Time.unscaledTime;
            if(landClip==null)landClip=Thud("Eşya yere indi",.09f,170,.4f);
            AudioSource.PlayClipAtPoint(landClip,position,.22f);
        }
        // Gloved fingers closing on a piece.
        public static void Grip(Vector3 position)
        {
            if(Application.isBatchMode||Time.unscaledTime-lastGrip<.1f)return;
            lastGrip=Time.unscaledTime;
            if(gripClip==null)gripClip=Thud("Kavrama",.06f,330,.7f);
            AudioSource.PlayClipAtPoint(gripClip,position,.18f);
        }
        public static void Celebrate()
        {
            if(Application.isBatchMode)return;
            if(winClip==null)winClip=Tone("Depo tamamlandı",.65f,440,880);
            var player=WarehousePlayer.Local;
            if(player!=null)AudioSource.PlayClipAtPoint(winClip,player.transform.position+Vector3.up*1.5f,.18f);
        }
        // Pickup, placement and drop cues driven by item location changes.
        public static void ItemSound(ItemLocation from,ItemLocation to,Vector3 position)
        {
            if(Application.isBatchMode||from==to)return;
            // Placing ten items at once must not stack ten identical clips.
            if(Time.unscaledTime-lastItemSound<.05f)return;
            lastItemSound=Time.unscaledTime;
            if(to==ItemLocation.Held)
            {
                if(pickClip==null)pickClip=Thud("Eşya alındı",.08f,520,.25f);
                AudioSource.PlayClipAtPoint(pickClip,position,.3f);
            }
            else if(to==ItemLocation.Stacked)
            {
                if(placeClip==null)placeClip=Thud("Eşya yerleşti",.14f,210,.35f);
                AudioSource.PlayClipAtPoint(placeClip,position,.4f);
            }
            else if(from==ItemLocation.Held&&to==ItemLocation.Loose)
            {
                if(dropClip==null)dropClip=Thud("Eşya bırakıldı",.16f,120,.5f);
                AudioSource.PlayClipAtPoint(dropClip,position,.35f);
            }
        }
        public static void Step(Vector3 position)
        {
            if(Application.isBatchMode)return;
            if(Time.unscaledTime-lastStep<.18f)return;
            lastStep=Time.unscaledTime;
            if(stepClip==null)stepClip=Thud("Adım",.07f,85,.6f);
            AudioSource.PlayClipAtPoint(stepClip,position,.12f);
        }
        // Eight seconds of slow pads. Every partial and LFO completes whole cycles, so the loop is seamless.
        public static AudioClip AmbientClip()
        {
            if(ambientClip!=null)return ambientClip;
            const int rate=22050;const int seconds=8;
            var samples=new float[rate*seconds];
            float[] partials={55f,82.5f,110f,165f};
            for(int i=0;i<samples.Length;i++)
            {
                double value=0;
                for(int k=0;k<partials.Length;k++)
                {
                    double tone=System.Math.Sin(2*System.Math.PI*partials[k]*i/rate+k);
                    double swell=.55+.45*System.Math.Sin(2*System.Math.PI*(k+1)*i/(double)(rate*seconds));
                    value+=tone*swell;
                }
                samples[i]=(float)(value*.5/partials.Length);
            }
            ambientClip=AudioClip.Create("Depo ortamı",samples.Length,1,rate,false);ambientClip.SetData(samples,0);
            return ambientClip;
        }
        // Rusty hinge: a falling saw wave with a wobble and some grit.
        static AudioClip Creak()
        {
            const int rate=22050;const float duration=.6f;var samples=new float[Mathf.CeilToInt(duration*rate)];float phase=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/samples.Length;
                float frequency=Mathf.Lerp(150,62,t)*(1+.05f*Mathf.Sin(t*60));
                phase+=frequency/rate;phase-=Mathf.Floor(phase);
                float saw=phase*2-1,wobble=.55f+.45f*Mathf.Sin(t*38),grit=(Random.value*2-1)*.25f;
                samples[i]=(saw*.5f+grit)*wobble*Mathf.Sin(t*Mathf.PI)*.55f;
            }
            var clip=AudioClip.Create("Menteşe",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        static AudioClip Tone(string name,float duration,float from,float to)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(duration*rate)];float phase=0;
            for(int i=0;i<samples.Length;i++){float t=(float)i/samples.Length;phase+=2*Mathf.PI*Mathf.Lerp(from,to,t)/rate;samples[i]=Mathf.Sin(phase)*Mathf.Sin(t*Mathf.PI)*(1-t)*.6f;}
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        // Short percussive cue: a decaying tone mixed with noise.
        static AudioClip Thud(string name,float duration,float frequency,float noise)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(duration*rate)];float phase=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/samples.Length,envelope=(1-t)*(1-t);
                phase+=2*Mathf.PI*Mathf.Lerp(frequency,frequency*.6f,t)/rate;
                samples[i]=(Mathf.Sin(phase)*(1-noise)+(Random.value*2-1)*noise)*envelope*.7f;
            }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
    }
}
