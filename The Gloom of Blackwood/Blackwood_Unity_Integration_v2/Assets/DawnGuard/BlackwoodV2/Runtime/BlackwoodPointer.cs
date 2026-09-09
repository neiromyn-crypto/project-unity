using UnityEngine;
using UnityEngine.EventSystems;

namespace DawnGuard.BlackwoodV2
{
    public sealed class BlackwoodPointer : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler
    {
        public BlackwoodRoot root;
        public void OnPointerClick(PointerEventData e) { if(root!=null) root.HandleBoardClick(e.position); }
        public void OnPointerMove(PointerEventData e) { if(root!=null) root.PreviewAt(e.position); }
    }
}
