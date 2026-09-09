using DG.Tweening;
using Match3.Content;
using Match3.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Gameplay
{
    /// <summary>
    /// One obstacle on screen: a base sprite per health stage plus tinted bow and next-colour pip
    /// overlays. Display only (rule V1).
    /// </summary>
    public sealed class ElementView : MonoBehaviour
    {
        [SerializeField] private Image _base;
        [SerializeField] private Image _bow;
        [SerializeField] private Image _pip;
        [SerializeField] private RectTransform _rect;

        private ElementVisualProfile _elementProfile;
        private ChipVisualProfile _chipProfile;
        private ElementVisualProfile.ElementVisual _visual;
        private int _maxHealth;

        public RectTransform Rect => _rect;

        public string Token { get; private set; }

        public void Initialise(ElementVisualProfile elementProfile, ChipVisualProfile chipProfile)
        {
            _elementProfile = elementProfile;
            _chipProfile = chipProfile;
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            _base.raycastTarget = false;
            _bow.raycastTarget = false;
            _pip.raycastTarget = false;
        }

        public void Show(
            string token,
            int health,
            int maxHealth,
            ChipColor color,
            ChipColor nextColor,
            Vector2 anchoredPosition,
            float cellSize)
        {
            Token = token;
            _maxHealth = maxHealth;
            _elementProfile.TryGet(token, out _visual);

            _rect.sizeDelta = new Vector2(cellSize, cellSize);
            _rect.anchoredPosition = anchoredPosition;
            _rect.localScale = Vector3.one;

            SetHealth(health);
            SetColor(color, nextColor);
        }

        /// <summary>§7.1: the visual must change on every hit point lost.</summary>
        public void SetHealth(int health)
        {
            _base.sprite = _visual != null ? _visual.SpriteForHealth(health, _maxHealth) : null;
            _base.enabled = _base.sprite != null;
        }

        public void SetColor(ChipColor color, ChipColor nextColor)
        {
            bool showBow = _visual != null && _visual.ShowBow && color != ChipColor.None;
            _bow.enabled = showBow;
            if (showBow)
            {
                _bow.sprite = _elementProfile.BowOverlay;
                _bow.color = TintFor(color);
            }

            bool showPip = _visual != null && _visual.ShowNextColorPip && nextColor != ChipColor.None;
            _pip.enabled = showPip;
            if (showPip)
            {
                _pip.sprite = _elementProfile.PipOverlay;
                _pip.color = TintFor(nextColor);
            }
        }

        public void KillTweens()
        {
            _rect.DOKill();
            _base.DOKill();
            _bow.DOKill();
            _pip.DOKill();
        }

        private Color TintFor(ChipColor color)
            => _chipProfile != null ? _chipProfile.GetParticleColor(color) : Color.white;
    }
}
