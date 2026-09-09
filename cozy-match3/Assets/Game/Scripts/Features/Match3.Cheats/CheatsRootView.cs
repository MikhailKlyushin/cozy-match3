using UnityEngine;

namespace Match3.Cheats
{
    /// <summary>
    /// Root of the cheats prefab: the panel window, the full-screen coordinate overlay and the
    /// full-screen tap catcher. One component for the installer to bind (T23) instead of three
    /// scene references, and the prefab lives in Assets/Game/Content/Cheats - never in Resources,
    /// from where it would ship in the release build regardless of the define (§15).
    /// </summary>
    public sealed class CheatsRootView : MonoBehaviour
    {
        [SerializeField] private CheatPanelView _panel;
        [SerializeField] private CheatGridOverlayView _gridOverlay;
        [SerializeField] private CheatBoardTapCatcher _tapCatcher;

        public CheatPanelView Panel => _panel;

        public CheatGridOverlayView GridOverlay => _gridOverlay;

        public CheatBoardTapCatcher TapCatcher => _tapCatcher;
    }
}
