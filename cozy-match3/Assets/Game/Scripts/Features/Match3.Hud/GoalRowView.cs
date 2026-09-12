using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Hud
{
    /// <summary>
    /// One goal row: icon plus current/target, and a tick once the goal closes (§11.2). A closed
    /// row stops pulsing.
    /// </summary>
    public sealed class GoalRowView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _counterLabel;
        [SerializeField] private Image _closedTick;
        [SerializeField] private RectTransform _pulseTarget;
        [SerializeField] private float _tickPulseScale = 1.15f;

        private readonly StringBuilder _counterText = new StringBuilder(12);

        private int _target;
        private bool _closed;

        public bool IsClosed => _closed;

        public void Show(Sprite icon, int value, int target)
        {
            _target = target;

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            KillTweens();
            SetValue(value);
        }

        public void SetValue(int value)
        {
            _closed = _target > 0 && value >= _target;

            if (_counterLabel != null)
            {
                _counterText.Length = 0;
                _counterText.Append(value);
                _counterText.Append('/');
                _counterText.Append(_target);
                _counterLabel.text = _counterText.ToString();
            }

            if (_closedTick != null)
            {
                _closedTick.enabled = _closed;
            }

            if (_closed)
            {
                KillTweens();
            }
        }

        /// <summary>One pulse per counted unit; skipped once the goal is closed (§11.2).</summary>
        public void PlayTick(float duration)
        {
            if (_closed || duration <= 0f || _pulseTarget == null)
            {
                return;
            }

            float punch = _tickPulseScale - 1f;
            _pulseTarget
                .DOPunchScale(new Vector3(punch, punch, 0f), duration, vibrato: 1, elasticity: 0f)
                .SetLink(gameObject);
        }

        public void KillTweens()
        {
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

            if (_icon != null)
            {
                _icon.raycastTarget = false;
            }

            if (_counterLabel != null)
            {
                _counterLabel.raycastTarget = false;
            }

            if (_closedTick != null)
            {
                _closedTick.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
