using UnityEngine;
using UnityEngine.EventSystems;

namespace DawnGuard.Unity
{
    public sealed class BoardPointer : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler
    {
        public GameRoot root;
        public void OnPointerClick(PointerEventData data)
        { if(root!=null) root.HandleBoardClick(data.position); }
        public void OnPointerMove(PointerEventData data)
        { if(root!=null) root.PreviewAt(data.position); }
    }
}
