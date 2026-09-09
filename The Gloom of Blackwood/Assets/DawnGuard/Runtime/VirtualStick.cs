using UnityEngine;
using UnityEngine.EventSystems;

namespace DawnGuard.Unity
{
    public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform handle;
        public Vector2 Value { get; private set; }
        private int pointerId=int.MinValue;
        public void OnPointerDown(PointerEventData data)
        {
            if(pointerId!=int.MinValue) return;
            pointerId=data.pointerId; UpdateStick(data);
        }
        public void OnDrag(PointerEventData data) { if(data.pointerId==pointerId) UpdateStick(data); }
        public void OnPointerUp(PointerEventData data) { if(data.pointerId==pointerId) ResetInput(); }
        private void UpdateStick(PointerEventData data)
        {
            var rect=(RectTransform)transform;
            Vector2 p;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out p);
            float radius=rect.rect.width*0.35f;
            Value=Vector2.ClampMagnitude(p/radius,1);
            if(handle!=null) handle.anchoredPosition=Value*radius;
        }
        public void ResetInput()
        { pointerId=int.MinValue; Value=Vector2.zero; if(handle!=null) handle.anchoredPosition=Vector2.zero; }
        private void OnDisable() { ResetInput(); }
    }
}
