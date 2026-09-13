using System;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Hud
{
    /// <summary>
    /// Restart and Cheats buttons, top right (§11.2). Plain events: one consumer each and no
    /// composition, which §13 prefers over a Subject per field. The cheats button exists whether
    /// or not MATCH3_CHEATS is defined, so this view never references Match3.Cheats; whoever owns
    /// the panel calls <see cref="SetCheatsAvailable"/> to say whether it is there.
    /// </summary>
    public sealed class HudActionsView : MonoBehaviour
    {
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _cheatsButton;

        public event Action RestartClicked;

        public event Action CheatsClicked;

        /// <summary>
        /// A release build compiles the panel out (§15), so the button is hidden rather than left
        /// on screen as a dead control.
        /// </summary>
        public void SetCheatsAvailable(bool value)
        {
            if (_cheatsButton != null)
            {
                _cheatsButton.gameObject.SetActive(value);
            }
        }

        public void SetInteractable(bool value)
        {
            if (_restartButton != null)
            {
                _restartButton.interactable = value;
            }

            if (_cheatsButton != null)
            {
                _cheatsButton.interactable = value;
            }
        }

        private void Awake()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (_cheatsButton != null)
            {
                _cheatsButton.onClick.AddListener(OnCheatsClicked);
            }
        }

        private void OnDestroy()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (_cheatsButton != null)
            {
                _cheatsButton.onClick.RemoveListener(OnCheatsClicked);
            }

            RestartClicked = null;
            CheatsClicked = null;
        }

        private void OnRestartClicked()
        {
            RestartClicked?.Invoke();
        }

        private void OnCheatsClicked()
        {
            CheatsClicked?.Invoke();
        }
    }
}
