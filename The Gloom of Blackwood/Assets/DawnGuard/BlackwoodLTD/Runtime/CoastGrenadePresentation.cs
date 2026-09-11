using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGuard.BlackwoodLTD
{
    // One reused grenade and two reused rings. No lights, physics bodies or frame-time asset lookup.
    public sealed class CoastGrenadePresentation : MonoBehaviour
    {
        CoastRoot root;Transform drone;GameObject model;LineRenderer preview,blast;float flash;Transform pulse;Transform[] smoke=new Transform[3];
        public Transform Visual => model.transform;
        public bool PreviewVisible => preview.enabled;
        public void Initialize(CoastRoot owner,Transform holder,Transform parent,GameObject prefab,Material amber,Material soot)
        {
            root=owner;drone=holder;model=CoastWorld.Place(prefab,parent,Vector3.zero,.6f,.85f);model.name="DroneGrenade";model.SetActive(false);
            preview=Ring("Grenade AoE preview",parent,amber,.045f);blast=Ring("Grenade blast placeholder",parent,amber,.18f);
            pulse=Puff("Explosion flash",parent,amber);for(int i=0;i<smoke.Length;i++)smoke[i]=Puff("Smoke puff "+i,parent,soot);
        }
        static Transform Puff(string name,Transform parent,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(parent,false);var collider=go.GetComponent<Collider>();if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;go.SetActive(false);return go.transform;
        }
        static LineRenderer Ring(string name,Transform parent,Material material,float width)
        {
            var go=new GameObject(name,typeof(LineRenderer));go.transform.SetParent(parent,false);var l=go.GetComponent<LineRenderer>();l.sharedMaterial=material;l.useWorldSpace=false;l.loop=true;l.positionCount=48;l.startWidth=l.endWidth=width;
            for(int i=0;i<48;i++){float a=i*Mathf.PI/24;l.SetPosition(i,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}
            l.shadowCastingMode=ShadowCastingMode.Off;l.receiveShadows=false;l.enabled=false;return l;
        }
        public void Preview(Vector3 p,bool visible){preview.enabled=visible;if(visible){preview.transform.position=p+Vector3.up*.055f;preview.transform.localScale=Vector3.one*root.Game.Rules.grenade.radius;}}
        public void OnEvent(CoastEvent e){if(e.kind=="grenade_explosion"){flash=.8f;blast.transform.position=new Vector3(e.x,.09f,e.z);}}
        public void Refresh(float dt)
        {
            var s=root.Game.S;bool night=s.phase==CoastPhase.Night;bool loaded=s.grenadeState==GrenadeState.LOADED;bool falling=s.grenadeState==GrenadeState.DROPPED;
            bool storage=night&&!loaded&&!falling&&s.grenadeCharges>0;
            model.SetActive(night&&(loaded||falling||storage));
            if(loaded){if(model.transform.parent!=drone)model.transform.SetParent(drone,false);model.transform.localPosition=new Vector3(0,-.75f,0);model.transform.localRotation=Quaternion.identity;}
            else
            {
                if(model.transform.parent==drone)model.transform.SetParent(transform.parent,true);
                if(falling){float t=1-s.grenadeTimer/root.Game.Rules.grenade.fallSeconds;model.transform.position=new Vector3(s.grenadeX,Mathf.Lerp(1.85f,.05f,t*t),s.grenadeZ);model.transform.rotation=Quaternion.Euler(t*150,0,t*35);}
                else if(storage){model.transform.position=new Vector3(root.Game.Map.DronePadX,.24f,root.Game.Map.DronePadZ);model.transform.rotation=Quaternion.identity;}
            }
            if(!night||!root.GrenadeTargeting||root.Paused)preview.enabled=false;
            if(!root.Paused)flash=Mathf.Max(0,flash-dt);blast.enabled=flash>0;
            if(blast.enabled){float t=1-flash/.8f;blast.transform.localScale=Vector3.one*Mathf.Lerp(.15f,root.Game.Rules.grenade.radius,t);blast.widthMultiplier=Mathf.Lerp(2,0,t);}
            pulse.gameObject.SetActive(flash>.67f);if(flash>.67f){pulse.position=blast.transform.position+Vector3.up*.4f;pulse.localScale=Vector3.one*1.4f;}
            for(int i=0;i<smoke.Length;i++){smoke[i].gameObject.SetActive(flash>0);if(flash<=0)continue;float t=1-flash/.8f;smoke[i].position=blast.transform.position+new Vector3((i-1)*.45f,.3f+t*(1+i*.2f),i%2*.3f);smoke[i].localScale=Vector3.one*(.3f+Mathf.Sin(t*Mathf.PI)*1.2f);}
        }
        public void Reset(){flash=0;preview.enabled=blast.enabled=false;pulse.gameObject.SetActive(false);foreach(var puff in smoke)puff.gameObject.SetActive(false);model.SetActive(false);}
    }
}
