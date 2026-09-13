using System;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Hud
{
    /// <summary>
    /// Sound on/off button, bottom right (§11.2). Input and display only: the muted state belongs
    /// to whoever owns the audio and comes back through <see cref="SetMuted"/>.
    /// </summary>
    public sealed class SoundToggleView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private Sprite _enabledSprite;
        [SerializeField] private Sprite _disabledSprite;

        public event Action Clicked;

        public void SetMuted(bool muted)
        {
            Sprite sprite = muted ? _disabledSprite : _enabledSprite;
            if (_icon != null && sprite != null)
            {
                _icon.sprite = sprite;
            }
        }

        private void Awake()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_icon == null)
            {
                _icon = GetComponent<Image>();
            }

            if (_button != null)
            {
                _button.onClick.AddListener(OnClicked);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
            }

            Clicked = null;
        }

        private void OnClicked()
        {
            Clicked?.Invoke();
        }
    }
}
