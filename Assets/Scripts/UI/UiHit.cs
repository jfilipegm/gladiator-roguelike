using UnityEngine;
using UnityEngine.EventSystems;

namespace Gladiator.UI
{
    public interface IUiHitTarget
    {
        void Hover(int id);
        void Click(int id);
    }

    /// <summary>
    /// Pointer target that forwards hover/click with an id to its owner. Used instead of Button: no UnityEvent
    /// closures, no Selectable colour tweens (those allocated on every show/hide).
    /// </summary>
    public sealed class UiHit : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public IUiHitTarget Target;
        public int Id;

        public void OnPointerEnter(PointerEventData e) => Target?.Hover(Id);
        public void OnPointerClick(PointerEventData e) => Target?.Click(Id);
    }
}
