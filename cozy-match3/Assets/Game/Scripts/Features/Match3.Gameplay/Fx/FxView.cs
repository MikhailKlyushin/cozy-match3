using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Gameplay
{
    /// <summary>
    /// One pooled FX instance: a sprite, a particle system, or both. Always rented from and
    /// returned to <see cref="FxRegistry"/> - an Instantiate inside a cascade is a visible frame
    /// drop on WebGL (§14). Display only, like every other view (rule V1).
    /// </summary>
    public sealed class FxView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private Image _image;
        [SerializeField] private ParticleSystem _particles;
        [SerializeField] private FxFlipbook _flipbook;

        /// <summary>
        /// Share of the sprite frame the drawing actually covers. Every size a caller gives is in
        /// cells and means the size it wants to SEE, but the pack's textures leave a wide empty
        /// margin - a beam fills a tenth of its frame's height - so a size handed straight to
        /// <c>sizeDelta</c> comes out that much smaller on screen. Dividing by the fill undoes it.
        /// </summary>
        [SerializeField] private Vector2 _spriteFill = Vector2.one;

        /// <summary>
        /// Colour the prefab was authored with. Every caller rents with <c>Color.white</c> because
        /// §5.1 puts the colour in the effect, not in the call - and with greyscale source
        /// textures that colour has to live somewhere. It lives here, and the rented tint
        /// multiplies it, so a white prefab behaves exactly as it did before.
        /// </summary>
        private Color _authoredTint = Color.white;

        private const float Half = 0.5f;

        public RectTransform Rect => _rect;

        private void Awake()
        {
            if (_image != null)
            {
                _authoredTint = _image.color;
            }
        }

        /// <summary>
        /// Places, resizes and restarts the effect, without a sheet duration - a caller that has
        /// no timing to give leaves a flipbook on its first frame rather than guessing one.
        /// </summary>
        public void Prepare(Vector2 anchoredPosition, Vector2 size, Color tint)
            => Prepare(anchoredPosition, size, tint, 0f);

        /// <summary>
        /// Places, resizes and restarts the effect. Every rent goes through here.
        /// <paramref name="duration"/> is how long a <see cref="FxFlipbook"/> on the prefab runs
        /// its sheet; it comes from the caller's <c>TimingProfile</c>, never from the prefab.
        /// </summary>
        public void Prepare(Vector2 anchoredPosition, Vector2 size, Color tint, float duration)
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            KillTweens();

            _rect.anchoredPosition = anchoredPosition;
            _rect.sizeDelta = Fitted(size);
            _rect.localScale = Vector3.one;
            _rect.localRotation = Quaternion.identity;

            if (_image != null)
            {
                _image.color = tint * _authoredTint;
                _image.raycastTarget = false;
            }

            if (_particles != null)
            {
                _particles.Clear(true);
                _particles.Play(true);
            }

            if (_flipbook != null)
            {
                _flipbook.Play(duration);
            }
        }

        /// <summary>Zero-length beam anchored at <paramref name="from"/> and aimed at <paramref name="to"/>.</summary>
        public void PrepareBeam(Vector2 from, Vector2 to, float thickness, Color tint)
        {
            Prepare(from, new Vector2(0f, thickness), tint);

            Vector2 delta = to - from;
            if (delta.sqrMagnitude > Mathf.Epsilon)
            {
                SetRotation(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>Grows a prepared beam to full length; its near end stays on the origin.</summary>
        public void ExtendBeam(Vector2 from, Vector2 to, float thickness, float duration)
        {
            Vector2 delta = to - from;
            MoveTo(from + delta * Half, duration);
            SizeTo(new Vector2(delta.magnitude, thickness), duration);
        }

        public void SetPosition(Vector2 anchoredPosition) => _rect.anchoredPosition = anchoredPosition;

        public void SetRotation(float degrees) => _rect.localRotation = Quaternion.Euler(0f, 0f, degrees);

        public void MoveTo(Vector2 anchoredPosition, float duration)
        {
            if (duration <= 0f)
            {
                _rect.anchoredPosition = anchoredPosition;
                return;
            }

            _rect.DOAnchorPos(anchoredPosition, duration).SetEase(Ease.Linear).SetLink(gameObject);
        }

        /// <summary>Linear, so a growing beam's far end stays exactly under the beam head.</summary>
        public void SizeTo(Vector2 size, float duration) => SizeTo(size, duration, Ease.Linear);

        /// <summary>An expanding flash takes <see cref="Ease.OutQuad"/>: it has no head to follow.</summary>
        public void SizeTo(Vector2 size, float duration, Ease ease)
        {
            Vector2 target = Fitted(size);

            if (duration <= 0f)
            {
                _rect.sizeDelta = target;
                return;
            }

            _rect.DOSizeDelta(target, duration).SetEase(ease).SetLink(gameObject);
        }

        public void ScaleTo(float scale, float duration)
        {
            if (duration <= 0f)
            {
                _rect.localScale = new Vector3(scale, scale, 1f);
                return;
            }

            _rect.DOScale(scale, duration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        public void FadeOut(float duration)
        {
            if (_image == null)
            {
                return;
            }

            if (duration <= 0f)
            {
                Color color = _image.color;
                color.a = 0f;
                _image.color = color;
                return;
            }

            _image.DOFade(0f, duration).SetLink(gameObject);
        }

        /// <summary>Called on release, so a pooled instance always comes back clean.</summary>
        public void Stop()
        {
            KillTweens();

            if (_particles != null)
            {
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (_flipbook != null)
            {
                _flipbook.Stop();
            }

            if (_rect != null)
            {
                _rect.localScale = Vector3.one;
                _rect.localRotation = Quaternion.identity;
            }
        }

        public void KillTweens()
        {
            if (_rect != null)
            {
                _rect.DOKill();
            }

            if (_image != null)
            {
                _image.DOKill();
            }
        }

        /// <summary>Zero or negative fill means the prefab was authored without one: pass it through.</summary>
        private Vector2 Fitted(Vector2 size)
            => new Vector2(
                _spriteFill.x > 0f ? size.x / _spriteFill.x : size.x,
                _spriteFill.y > 0f ? size.y / _spriteFill.y : size.y);
    }
}
