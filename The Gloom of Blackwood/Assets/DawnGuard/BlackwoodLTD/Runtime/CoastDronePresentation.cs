using UnityEngine;
using UnityEngine.Rendering;

namespace DawnGuard.BlackwoodLTD
{
    // Presentation only: the existing drone holder and simulation coordinates stay unchanged.
    public sealed class CoastDronePresentation : MonoBehaviour
    {
        public Transform Visual {get;private set;}
        public Transform GroundMarker {get;private set;}
        public Transform OverheadMarker {get;private set;}
        public Vector3 Position => transform.position + Vector3.up * .55f;
        Transform cameraTransform;
        Material groundMaterial;
        Mesh groundMesh;

        public void Initialize(Transform markerParent,Camera camera,Material teal,Material amber)
        {
            cameraTransform=camera.transform;
            var model=transform.GetChild(0);
            Visual=new GameObject("Visual").transform;Visual.SetParent(transform,false);
            model.SetParent(Visual,false);
            Visual.localScale=Vector3.one*1.3f;
            Visual.localPosition=Vector3.up*.25f;

            // A narrow transparent annulus on the existing flat y=0 coast. No physics query.
            groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Drone ground teal"};
            groundMaterial.SetColor("_BaseColor",new Color(.30f,.91f,.83f,.48f));
            groundMaterial.SetFloat("_Surface",1);groundMaterial.SetFloat("_Blend",0);
            groundMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            groundMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            groundMaterial.SetFloat("_ZWrite",0);groundMaterial.SetFloat("_Cull",(float)CullMode.Off);
            groundMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            groundMaterial.SetOverrideTag("RenderType","Transparent");groundMaterial.renderQueue=(int)RenderQueue.Transparent;
            const int segments=48;
            var vertices=new Vector3[segments*2];var triangles=new int[segments*6];
            for(int i=0;i<segments;i++)
            {
                float angle=i*2*Mathf.PI/segments;var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                vertices[i*2]=direction*.78f;vertices[i*2+1]=direction*.88f;
                int n=(i+1)%segments*2,t=i*6;
                triangles[t]=i*2;triangles[t+1]=n;triangles[t+2]=i*2+1;
                triangles[t+3]=i*2+1;triangles[t+4]=n;triangles[t+5]=n+1;
            }
            groundMesh=new Mesh{name="Drone ground ring (96 triangles)"};
            groundMesh.vertices=vertices;groundMesh.triangles=triangles;groundMesh.RecalculateBounds();
            var ground=new GameObject("Drone ground marker",typeof(MeshFilter),typeof(MeshRenderer));
            GroundMarker=ground.transform;GroundMarker.SetParent(markerParent,false);
            ground.GetComponent<MeshFilter>().sharedMesh=groundMesh;
            var renderer=ground.GetComponent<MeshRenderer>();renderer.sharedMaterial=groundMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;

            float top=transform.position.y+.9f;
            foreach(var r in Visual.GetComponentsInChildren<Renderer>())top=Mathf.Max(top,r.bounds.max.y);
            OverheadMarker=new GameObject("Drone overhead diamond").transform;OverheadMarker.SetParent(transform,false);
            OverheadMarker.position=new Vector3(transform.position.x,top+.8f,transform.position.z);
            var diamond=Stroke(OverheadMarker,"Teal diamond",teal,.038f);
            diamond.loop=true;diamond.positionCount=4;
            diamond.SetPositions(new[]{new Vector3(0,.23f,0),new Vector3(.17f,0,0),new Vector3(0,-.23f,0),new Vector3(-.17f,0,0)});
            var accent=Stroke(OverheadMarker,"Amber accent",amber,.065f);accent.positionCount=2;
            accent.SetPositions(new[]{new Vector3(0,-.055f,0),new Vector3(0,.055f,0)});
            Refresh();
        }

        static LineRenderer Stroke(Transform parent,string name,Material material,float width)
        {
            var go=new GameObject(name,typeof(LineRenderer));go.transform.SetParent(parent,false);
            var line=go.GetComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;
            line.startWidth=line.endWidth=width;line.numCapVertices=0;line.numCornerVertices=0;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return line;
        }

        public void Refresh()
        {
            var p=transform.position;GroundMarker.position=new Vector3(p.x,.035f,p.z);
            OverheadMarker.rotation=cameraTransform.rotation;
        }

        void OnDestroy()
        {
            Release(groundMesh);Release(groundMaterial);
            if(GroundMarker!=null)Release(GroundMarker.gameObject);
        }
        static void Release(Object asset){if(asset==null)return;if(Application.isPlaying)Destroy(asset);else DestroyImmediate(asset);}
    }
}
