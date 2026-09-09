using UnityEngine;
using UnityEngine.UI;

namespace DawnGuard.BlackwoodV2
{
    // Native vector panel: no imported UI atlas, no extra package or material.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BlackwoodPanel : MaskableGraphic
    {
        public float corner=12;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            float c=Mathf.Min(corner,Mathf.Min(r.width,r.height)*.35f);
            Vector2[] points={new Vector2(r.xMin+c,r.yMin),new Vector2(r.xMax-c,r.yMin),
                new Vector2(r.xMax,r.yMin+c),new Vector2(r.xMax,r.yMax-c),
                new Vector2(r.xMax-c,r.yMax),new Vector2(r.xMin+c,r.yMax),
                new Vector2(r.xMin,r.yMax-c),new Vector2(r.xMin,r.yMin+c)};
            vh.AddVert(r.center,color,new Vector2(.5f,.5f));
            foreach(var p in points) vh.AddVert(p,color,Vector2.zero);
            for(int i=0;i<8;i++) vh.AddTriangle(0,i+1,(i+1)%8+1);
        }
    }
}
