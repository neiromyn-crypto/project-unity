using UnityEngine;
namespace DawnGuard.BlackwoodLTD
{
    // Camera intent is independent of world visibility. A later scouting system can
    // request Focus without coupling visibility/fog to pan or zoom implementation.
    public sealed class CoastCameraRig
    {
        public Vector3 Focus {get;private set;}
        public float Size {get;private set;}=10;
        readonly float width,depth;
        public CoastCameraRig(CoastMap map){width=map.Width;depth=map.Depth;Focus=new Vector3(map.CampX,0,10.5f);}
        public void SetFocus(Vector3 desired){Focus=Clamp(desired);}
        public Vector3 Clamp(Vector3 desired){return new Vector3(Mathf.Clamp(desired.x,4,width-4),0,Mathf.Clamp(desired.z,5,depth-3));}
        public void Zoom(float delta){Size=Mathf.Clamp(Size+delta,8,13);}
        public void PanPixels(Vector2 delta,float pixelsHigh)
        {float scale=2*Size/Mathf.Max(1,pixelsHigh);SetFocus(Focus-new Vector3(delta.x*scale,0,delta.y*scale/Mathf.Sin(50*Mathf.Deg2Rad)));}
        public void Move(Vector2 direction,float dt){SetFocus(Focus+new Vector3(direction.x,0,direction.y)*Mathf.Max(0,dt)*13);}
    }
}
