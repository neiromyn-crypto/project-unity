using System.Collections.Generic;
using DawnGuard.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DawnGuard.BlackwoodV2
{
    // Scene presentation only. Simulation time, damage and power allocation remain in the core.
    public sealed class BlackwoodLighting : MonoBehaviour
    {
        BlackwoodRoot root;
        BlackwoodLightingProfile settings;
        Light sun,skyFill;
        Volume volume;
        RenderPipelineAsset previousPipeline;
        AmbientMode previousAmbientMode;
        Color previousSky,previousEquator,previousGround;
        SphericalHarmonicsL2 previousProbe;
        bool previousFog;
        readonly Dictionary<int,Light> lamps=new Dictionary<int,Light>();
        readonly List<int> expired=new List<int>();
        float level;
        public float NightBlend {get {return level;}}
        public int LocalLightCount {get {return lamps.Count;}}
        public void Initialize(BlackwoodRoot owner,Camera camera,Light key)
        {
            root=owner; settings=root.lightingProfile; sun=key;
            previousPipeline=QualitySettings.renderPipeline;
            if(settings.pipeline!=null) QualitySettings.renderPipeline=settings.pipeline;
            previousAmbientMode=RenderSettings.ambientMode; previousSky=RenderSettings.ambientSkyColor;
            previousEquator=RenderSettings.ambientEquatorColor; previousGround=RenderSettings.ambientGroundColor; previousFog=RenderSettings.fog;
            previousProbe=RenderSettings.ambientProbe;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            // The low orthographic view does not benefit from global distance fog.
            RenderSettings.fog=false;
            camera.allowHDR=true; camera.allowMSAA=true;
            var data=camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.None;
            volume=gameObject.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=20; volume.sharedProfile=settings.postProcessing;
            sun.shadowBias=.035f; sun.shadowNormalBias=.25f; sun.shadowStrength=.85f;
            skyFill=new GameObject("Soft sky fill").AddComponent<Light>();
            skyFill.transform.SetParent(transform,false); skyFill.type=LightType.Directional;
            skyFill.shadows=LightShadows.None; skyFill.transform.rotation=Quaternion.Euler(65,145,0);
            ApplyLight();
        }
        public void Refresh()
        {
            var game=root.Game;
            bool night=game.Phase==GamePhase.Night || game.Phase==GamePhase.Defeat;
            float target=night ? 1 : game.Day>1 ? Mathf.Clamp01(1-game.Remaining/settings.eveningLeadSeconds)*.5f : 0;
            if(!root.Paused) level=Mathf.MoveTowards(level,target,Time.unscaledDeltaTime/Mathf.Max(.1f,settings.transitionSeconds));
            ApplyLight();
            expired.Clear(); foreach(var pair in lamps) if(game.FindBuilding(pair.Key)==null) expired.Add(pair.Key);
            foreach(int id in expired) {Destroy(lamps[id].gameObject); lamps.Remove(id);}
            foreach(var b in game.Buildings)
            {
                if(b.definitionId!="shelter" && b.definitionId!="camp" && b.definitionId!="generator" && b.definitionId!="lab" && b.definitionId!="tesla") continue;
                Light lamp;
                if(!lamps.TryGetValue(b.instanceId,out lamp))
                {
                    if(lamps.Count>=settings.maximumLocalLights) continue;
                    lamp=new GameObject("Local light "+b.definitionId+" "+b.instanceId).AddComponent<Light>();
                    lamp.transform.SetParent(transform,false); lamp.type=LightType.Point; lamp.shadows=LightShadows.None;
                    lamp.range=settings.lampRange; lamp.renderMode=LightRenderMode.ForcePixel; lamps.Add(b.instanceId,lamp);
                }
                var definition=game.Rules.Building(b.definitionId);
                bool cool=b.definitionId=="generator" || b.definitionId=="tesla";
                lamp.color=cool ? settings.powerLamp : settings.warmLamp;
                lamp.intensity=(cool ? settings.powerIntensity : settings.warmIntensity)*Mathf.SmoothStep(.02f,1,level)*(b.powered ? 1 : .08f);
                lamp.transform.position=new Vector3(b.cell.x+definition.width*.5f,1.15f,b.cell.z-.25f);
            }
        }
        void ApplyLight()
        {
            float dusk=Mathf.Clamp01(level*2), night=Mathf.Clamp01((level-.5f)*2);
            sun.color=Color.Lerp(Color.Lerp(settings.daySun,settings.eveningSun,dusk),settings.moon,night);
            sun.intensity=Mathf.Lerp(Mathf.Lerp(settings.dayIntensity,settings.eveningIntensity,dusk),settings.moonIntensity,night);
            sun.transform.rotation=Quaternion.Euler(Mathf.Lerp(48,34,dusk),Mathf.Lerp(-38,-25,night),0);
            skyFill.color=Color.Lerp(new Color(.9f,.95f,1),new Color(.72f,.82f,1),level);
            skyFill.intensity=Mathf.Lerp(.42f,.16f,level);
            RenderSettings.ambientSkyColor=Color.Lerp(settings.daySky,settings.nightSky,level);
            RenderSettings.ambientEquatorColor=Color.Lerp(settings.dayEquator,settings.nightEquator,level);
            RenderSettings.ambientGroundColor=Color.Lerp(settings.dayGround,settings.nightGround,level);
            // Refresh diffuse sky fill explicitly: changing Trilight colors alone leaves a stale ambient probe in URP.
            var probe=new SphericalHarmonicsL2();
            probe.AddAmbientLight(Color.Lerp(settings.dayEquator,settings.nightEquator,level));
            probe.AddDirectionalLight(Vector3.up,Color.Lerp(settings.daySky,settings.nightSky,level),.35f);
            RenderSettings.ambientProbe=probe;
        }
        void OnDestroy()
        {
            if(settings==null) return;
            QualitySettings.renderPipeline=previousPipeline;
            RenderSettings.ambientMode=previousAmbientMode; RenderSettings.ambientSkyColor=previousSky;
            RenderSettings.ambientEquatorColor=previousEquator; RenderSettings.ambientGroundColor=previousGround; RenderSettings.fog=previousFog;
            RenderSettings.ambientProbe=previousProbe;
        }
    }
}
