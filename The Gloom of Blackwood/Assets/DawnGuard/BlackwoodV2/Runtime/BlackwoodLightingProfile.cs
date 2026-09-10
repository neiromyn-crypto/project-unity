using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGuard.BlackwoodV2
{
    [CreateAssetMenu(menuName="Dawn Guard/Blackwood lighting profile")]
    public sealed class BlackwoodLightingProfile : ScriptableObject
    {
        public UniversalRenderPipelineAsset pipeline;
        public VolumeProfile postProcessing;
        public Color daySun=new Color(1,.97f,.9f), eveningSun=new Color(1,.78f,.56f), moon=new Color(.80f,.87f,.96f);
        public float dayIntensity=1.35f, eveningIntensity=.9f, moonIntensity=.45f;
        public Color daySky=new Color(.42f,.49f,.53f), nightSky=new Color(.16f,.20f,.25f);
        public Color dayEquator=new Color(.28f,.31f,.29f), nightEquator=new Color(.11f,.14f,.17f);
        public Color dayGround=new Color(.16f,.18f,.14f), nightGround=new Color(.065f,.08f,.09f);
        public Color warmLamp=new Color(1,.77f,.48f), powerLamp=new Color(.42f,.85f,1);
        public float warmIntensity=4.2f, powerIntensity=3.1f, lampRange=4.8f;
        [Range(1,8)] public int maximumLocalLights=6;
        public float transitionSeconds=6, eveningLeadSeconds=20;
    }
}
