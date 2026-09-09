using UnityEngine;

namespace DawnGuard.Unity
{
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private Rect previous;
        private int width,height;
        private void Update()
        {
            var area=Screen.safeArea;
            if(area==previous && width==Screen.width && height==Screen.height) return;
            previous=area; width=Screen.width; height=Screen.height;
            if(width==0 || height==0) return;
            var rect=(RectTransform)transform;
            rect.anchorMin=new Vector2(area.xMin/width,area.yMin/height);
            rect.anchorMax=new Vector2(area.xMax/width,area.yMax/height);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
