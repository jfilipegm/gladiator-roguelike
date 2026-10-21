using UnityEngine;
using UnityEngine.EventSystems;

namespace Gladiator.UI
{
    public interface IUiHoverTarget
    {
        void Hover(int id, bool on);
    }

    /// <summary>
    /// Pointer enter/exit with an id, forwarded to its owner (R9: the roster's and the ladder's hover frame). The click
    /// stays on the tile's Button, so a click is never handled twice.
    /// </summary>
    public sealed class UiHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public IUiHoverTarget Target;
        public int Id;

        public void OnPointerEnter(PointerEventData e) => Target?.Hover(Id, true);
        public void OnPointerExit(PointerEventData e) => Target?.Hover(Id, false);
    }
}
