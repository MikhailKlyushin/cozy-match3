using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Hud
{
    /// <summary>
    /// Looping frame-by-frame sprite animation for a HUD decoration: the sheet runs once at
    /// <see cref="_framesPerSecond"/>, rests on <see cref="_holdFrame"/> for
    /// <see cref="_holdSeconds"/>, then starts over. Like <c>FxFlipbook</c> it keeps no clock of
    /// its own - one linear DOTween tween walks the whole cycle, so there is no <c>Update</c> and
    /// no <c>Animator</c> (`art-and-fx-guide` §4.5) and the loop dies with the object.
    /// </summary>
    public sealed class LoopedFlipbookView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Sprite[] _frames;
        [SerializeField, Min(0f)] private float _framesPerSecond = 12f;
        [SerializeField, Min(0f)] private float _holdSeconds = 2f;
        [SerializeField, Min(0)] private int _holdFrame;

        private TweenCallback<float> _onCycleTime;
        private Tween _tween;

        /// <summary>Frames the sheet carries; zero means the component is inert.</summary>
        public int FrameCount => _frames != null ? _frames.Length : 0;

        /// <summary>Seconds one full cycle takes: the sheet plus the hold that follows it.</summary>
        public float CycleDuration => PlayDuration + Mathf.Max(0f, _holdSeconds);

        private float PlayDuration => _framesPerSecond > 0f ? FrameCount / _framesPerSecond : 0f;

        /// <summary>
        /// Restarts the loop from the first frame. A cycle of zero length - no frames, no rate and
        /// no hold - shows the hold frame and animates nothing.
        /// </summary>
        public void Play()
        {
            KillTween();

            if (FrameCount == 0 || _image == null)
            {
                return;
            }

            SetCycleTime(0f);

            float cycle = CycleDuration;
            if (cycle <= 0f)
            {
                return;
            }

            // The setter is cached so a restart allocates no delegate.
            _onCycleTime = _onCycleTime ?? SetCycleTime;
            _tween = DOVirtual.Float(0f, cycle, cycle, _onCycleTime)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetLink(gameObject);
        }

        /// <summary>Stops the loop and leaves the object on its first frame.</summary>
        public void Stop()
        {
            KillTween();

            if (FrameCount > 0 && _image != null)
            {
                SetCycleTime(0f);
            }
        }

        /// <summary>
        /// Shows the frame that belongs to <paramref name="seconds"/> inside one cycle: the sheet
        /// while the playback lasts, the hold frame after it. Public so the mapping can be
        /// asserted in an edit-mode test without a running tween.
        /// </summary>
        public void SetCycleTime(float seconds)
        {
            if (FrameCount == 0 || _image == null)
            {
                return;
            }

            int last = _frames.Length - 1;
            int index = seconds < PlayDuration
                ? Mathf.Clamp(Mathf.FloorToInt(seconds * _framesPerSecond), 0, last)
                : Mathf.Clamp(_holdFrame, 0, last);

            _image.sprite = _frames[index];
        }

        private void Awake()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            if (_image != null)
            {
                _image.raycastTarget = false;
            }
        }

        private void OnEnable() => Play();

        private void OnDisable() => Stop();

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
