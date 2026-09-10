using UnityEngine;
namespace DawnGuard.BlackwoodLTD
{
    // Small original synthesized effects and coastal ambience; no third-party audio dependency.
    public sealed class CoastAudio : MonoBehaviour
    {
        CoastRoot root;AudioSource ambience,effects;AudioClip wind,click,shot,spell,delivery,alarm;float nextShot;
        public float Volume {get;private set;}=.65f;
        public void Initialize(CoastRoot owner)
        {
            root=owner;Volume=PlayerPrefs.GetFloat("BlackwoodCoast.Volume",.65f);ambience=gameObject.AddComponent<AudioSource>();effects=gameObject.AddComponent<AudioSource>();
            wind=Synth("Sea wind",10,0,0);click=Synth("Interface",.07f,680,1);shot=Synth("Turret",.085f,85,2);spell=Synth("Arcane",.28f,480,3);delivery=Synth("Stone delivery",.12f,920,1);alarm=Synth("Night signal",.8f,175,3);
            ambience.clip=wind;ambience.loop=true;ambience.Play();SetVolume(Volume);
        }
        AudioClip Synth(string name,float seconds,float frequency,int mode)
        {
            const int rate=22050;var samples=new float[Mathf.CeilToInt(seconds*rate)];var random=new System.Random(123+mode);float filtered=0;
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;float n=(float)random.NextDouble()*2-1;filtered=Mathf.Lerp(filtered,n,.025f);float envelope=mode==0?1:Mathf.Pow(1-i/(float)samples.Length,2);
                samples[i]=mode==0?(filtered*.3f+Mathf.Sin(t*.6283f)*filtered*.2f):mode==2?(n*.4f+Mathf.Sin(t*frequency*6.28f)*.3f)*envelope:Mathf.Sin(t*(frequency+Mathf.Sin(t*18)*80)*6.28f)*.25f*envelope;}
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void SetVolume(float volume){Volume=Mathf.Clamp01(volume);PlayerPrefs.SetFloat("BlackwoodCoast.Volume",Volume);if(ambience!=null)ambience.volume=Volume*.6f;if(effects!=null)effects.volume=Volume;}
        public void Click(){if(effects!=null)effects.PlayOneShot(click,.35f);}
        public void OnEvent(CoastEvent e)
        {
            if(e.kind=="delivery"){effects.PlayOneShot(delivery,.12f);}
            else if(e.kind=="night")effects.PlayOneShot(alarm,.8f);
            else if(e.kind=="gun"&&Time.unscaledTime>nextShot){nextShot=Time.unscaledTime+.06f;effects.PlayOneShot(shot,.25f);}
            else if((e.kind=="tesla"||e.kind=="arcane"||e.kind=="cryo")&&Time.unscaledTime>nextShot){nextShot=Time.unscaledTime+.1f;effects.PlayOneShot(spell,.2f);}
        }
        public void Refresh(float dt){if(ambience!=null)ambience.pitch=root.Game.S.phase==CoastPhase.Night?.82f:1;}
        void OnDestroy(){foreach(var clip in new[]{wind,click,shot,spell,delivery,alarm})if(clip!=null)Destroy(clip);}
    }
}
