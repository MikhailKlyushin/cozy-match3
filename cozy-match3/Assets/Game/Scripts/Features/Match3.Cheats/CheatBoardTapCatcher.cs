using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Match3.Cheats
{
    /// <summary>
    /// Full-screen click catcher used to place a booster (§12). It is a uGUI pointer handler on
    /// purpose: the panel has no hot keys and no legacy input, and while it is open the board's
    /// own input presenter drops everything (D05), so the tap has to arrive through the UI.
    /// Armed only while a booster is selected, so it never swallows a normal press.
    /// </summary>
    public sealed class CheatBoardTapCatcher : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Graphic _raycastArea;

        private bool _armed;

        /// <summary>Carries the screen position of the tap; the panel maps it to a cell.</summary>
        public event Action<Vector2> Tapped;

        public bool IsArmed => _armed;

        public void SetArmed(bool armed)
        {
            _armed = armed;

            if (_raycastArea == null)
            {
                return;
            }

            _raycastArea.raycastTarget = armed;
            _raycastArea.enabled = armed;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_armed || eventData == null)
            {
                return;
            }

            Tapped?.Invoke(eventData.position);
        }

        private void Awake()
        {
            if (_raycastArea == null)
            {
                _raycastArea = GetComponent<Graphic>();
            }

            SetArmed(false);
        }

        private void OnDestroy()
        {
            Tapped = null;
        }
    }
}
