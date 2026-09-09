using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Hud
{
    /// <summary>
    /// Move counter, large, top left (§11.2). Display only: the value arrives from
    /// <see cref="MovesCounterPresenter"/>, which never recomputes it.
    /// </summary>
    public sealed class MovesCounterView : MonoBehaviour
    {
        [SerializeField] private Text _valueLabel;
        [SerializeField] private Text _captionLabel;
        [SerializeField] private RectTransform _pulseTarget;
        [SerializeField] private float _pulseScale = 1.12f;

        private Tween _pulse;
        private bool _pulsing;

        public void SetValue(int movesLeft)
        {
            if (_valueLabel != null)
            {
                _valueLabel.text = movesLeft.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Pulses one cycle per <paramref name="pulseCycle"/> seconds while moves run low (§11.2).</summary>
        public void SetLowMoves(bool low, float pulseCycle)
        {
            if (low == _pulsing)
            {
                return;
            }

            _pulsing = low;

            if (!low || pulseCycle <= 0f)
            {
                KillTweens();
                return;
            }

            if (_pulseTarget == null)
            {
                return;
            }

            // A yoyo loop builds one §11.2 cycle out of two half-duration legs.
            _pulse = _pulseTarget
                .DOScale(_pulseScale, pulseCycle * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        public void KillTweens()
        {
            if (_pulse != null)
            {
                _pulse.Kill();
                _pulse = null;
            }

            if (_pulseTarget != null)
            {
                _pulseTarget.DOKill();
                _pulseTarget.localScale = Vector3.one;
            }
        }

        private void Awake()
        {
            if (_pulseTarget == null)
            {
                _pulseTarget = transform as RectTransform;
            }

            if (_captionLabel != null)
            {
                _captionLabel.text = HudStrings.MovesCaption;
                _captionLabel.raycastTarget = false;
            }

            if (_valueLabel != null)
            {
                _valueLabel.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
