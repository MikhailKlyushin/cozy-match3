using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Gameplay
{
    /// <summary>
    /// Frame-by-frame sprite animation for a pooled <see cref="FxView"/>. It has no clock of its
    /// own: the duration arrives from <c>TimingProfile</c> through the player that rents the
    /// effect, and the frames are stepped by a DOTween tween. That is the whole point - an
    /// <c>Animator</c> runs on its own schedule and drifts away from the ANIMATE barrier that
    /// holds gravity back until every destruction effect is done (§5.1).
    /// </summary>
    public sealed class FxFlipbook : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Sprite[] _frames;

        private TweenCallback<float> _onProgress;
        private Tween _tween;

        /// <summary>Frames the sheet carries; zero means the component is inert.</summary>
        public int FrameCount => _frames != null ? _frames.Length : 0;

        /// <summary>
        /// Runs the sheet once over <paramref name="duration"/> seconds. A duration of zero or
        /// less shows the first frame and animates nothing, which is what a caller that has no
        /// timing to give should get.
        /// </summary>
        public void Play(float duration)
        {
            KillTween();

            if (FrameCount == 0 || _image == null)
            {
                return;
            }

            SetProgress(0f);

            if (duration <= 0f)
            {
                return;
            }

            // The setter is cached: a rent inside a cascade must not allocate a delegate (§14).
            _onProgress = _onProgress ?? SetProgress;
            _tween = DOVirtual.Float(0f, 1f, duration, _onProgress)
                .SetEase(Ease.Linear)
                .SetLink(gameObject);
        }

        /// <summary>Called on release, so the pooled instance comes back on its first frame.</summary>
        public void Stop()
        {
            KillTween();

            if (FrameCount > 0 && _image != null)
            {
                SetProgress(0f);
            }
        }

        /// <summary>
        /// Shows the frame that belongs to <paramref name="progress"/> in 0..1. Public so the
        /// frame mapping can be asserted in an edit-mode test without a running tween.
        /// </summary>
        public void SetProgress(float progress)
        {
            if (FrameCount == 0 || _image == null)
            {
                return;
            }

            int last = _frames.Length - 1;
            int index = Mathf.Clamp(Mathf.FloorToInt(progress * _frames.Length), 0, last);
            _image.sprite = _frames[index];
        }

        private void OnDestroy() => KillTween();

        private void KillTween()
        {
            if (_tween != null && _tween.IsActive())
            {
                _tween.Kill();
            }

            _tween = null;
        }
    }
}
