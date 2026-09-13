using UnityEngine;
using UnityEngine.EventSystems;

namespace Match3.Cheats
{
    /// <summary>
    /// Drags the cheat window by its header strip. The panel covers a third of the screen, and
    /// what it hides - a goal row, the top of the board - has to be reachable without closing the
    /// panel and losing the armed booster. The window is clamped to the canvas: dragged past the
    /// edge it would take its own header, and with it this handle, out of reach.
    /// </summary>
    public sealed class CheatPanelDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private RectTransform _window;
        [SerializeField] private RectTransform _bounds;

        private readonly Vector3[] _windowCorners = new Vector3[4];
        private readonly Vector3[] _boundsCorners = new Vector3[4];

        private Vector2 _pointerOrigin;
        private Vector2 _windowOrigin;
        private bool _dragging;

        /// <summary>The space the window is positioned in - its own parent, not this handle.</summary>
        private RectTransform DragSpace => _window != null ? _window.parent as RectTransform : null;

        public void OnBeginDrag(PointerEventData eventData)
        {
            RectTransform space = DragSpace;
            _dragging = space != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    space,
                    eventData.position,
                    eventData.pressEventCamera,
                    out _pointerOrigin);

            if (_dragging)
            {
                _windowOrigin = _window.anchoredPosition;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    DragSpace,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 pointer))
            {
                return;
            }

            _window.anchoredPosition = _windowOrigin + (pointer - _pointerOrigin);
            ClampToBounds();
        }

        /// <summary>A browser window resized while the panel was closed must not strand it off-screen.</summary>
        private void OnEnable() => ClampToBounds();

        private void ClampToBounds()
        {
            if (_window == null || _bounds == null)
            {
                return;
            }

            _window.GetWorldCorners(_windowCorners);
            _bounds.GetWorldCorners(_boundsCorners);

            // Corners run bottom-left, top-left, top-right, bottom-right.
            float dx = 0f;
            if (_windowCorners[0].x < _boundsCorners[0].x)
            {
                dx = _boundsCorners[0].x - _windowCorners[0].x;
            }
            else if (_windowCorners[2].x > _boundsCorners[2].x)
            {
                dx = _boundsCorners[2].x - _windowCorners[2].x;
            }

            // The top edge wins over the bottom one: a window taller than the screen keeps its
            // header on-screen, so it can still be dragged and closed.
            float dy = 0f;
            if (_windowCorners[1].y > _boundsCorners[1].y)
            {
                dy = _boundsCorners[1].y - _windowCorners[1].y;
            }
            else if (_windowCorners[0].y < _boundsCorners[0].y)
            {
                dy = _boundsCorners[0].y - _windowCorners[0].y;
            }

            if (dx != 0f || dy != 0f)
            {
                _window.position += new Vector3(dx, dy, 0f);
            }
        }
    }
}
