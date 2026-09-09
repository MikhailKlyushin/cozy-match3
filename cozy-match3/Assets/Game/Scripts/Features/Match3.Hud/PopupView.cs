using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Hud
{
    /// <summary>
    /// Base popup: fade in, wait for the single action button, fade out. Visibility runs through a
    /// CanvasGroup instead of SetActive, so the button listener is wired exactly once and no layout
    /// is rebuilt while the popup opens (§11.3).
    /// </summary>
    public abstract class PopupView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private Text _primaryButtonLabel;

        // §11.3 specifies no popup timing, so these two stay tunable on the prefab instead of
        // becoming literals inside the tween.
        [SerializeField] private float _fadeDuration = 0.2f;
        [SerializeField] private float _openScaleFrom = 0.92f;

        private UniTaskCompletionSource _primaryPressed;

        public abstract PopupKind Kind { get; }

        public bool IsVisible { get; private set; }

        protected abstract string Title { get; }

        protected abstract string PrimaryButtonText { get; }

        /// <summary>
        /// Fills the popup before it opens. Called outside playback, so reading goal state here is
        /// legal (rule V1).
        /// </summary>
        public virtual void Present(PopupContent content, GoalIconResolver icons)
        {
            ApplyStaticText();
        }

        public async UniTask ShowAsync(CancellationToken ct)
        {
            KillTweens();
            CompletePending();

            IsVisible = true;
            _primaryPressed = new UniTaskCompletionSource();

            if (_panel != null)
            {
                _panel.localScale = Vector3.one * _openScaleFrom;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }

            if (_fadeDuration <= 0f)
            {
                ApplyOpenedState();
                return;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.DOFade(1f, _fadeDuration).SetLink(gameObject);
            }

            if (_panel != null)
            {
                _panel.DOScale(1f, _fadeDuration).SetEase(Ease.OutBack).SetLink(gameObject);
            }

            await UniTask.Delay(
                TimeSpan.FromSeconds(_fadeDuration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);

            ApplyOpenedState();
        }

        /// <summary>Completes when the action button is pressed; throws when the level is torn down.</summary>
        public UniTask WaitForPrimaryAsync(CancellationToken ct)
        {
            UniTaskCompletionSource pressed = _primaryPressed;
            if (pressed == null)
            {
                throw new InvalidOperationException("The popup is not open.");
            }

            return pressed.Task.AttachExternalCancellation(ct);
        }

        public async UniTask HideAsync(CancellationToken ct)
        {
            if (!IsVisible)
            {
                return;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            if (_fadeDuration > 0f)
            {
                KillTweens();

                if (_canvasGroup != null)
                {
                    _canvasGroup.DOFade(0f, _fadeDuration).SetLink(gameObject);
                }

                if (_panel != null)
                {
                    _panel.DOScale(_openScaleFrom, _fadeDuration).SetLink(gameObject);
                }

                await UniTask.Delay(
                    TimeSpan.FromSeconds(_fadeDuration),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);
            }

            HideImmediate();
        }

        public void HideImmediate()
        {
            KillTweens();
            CompletePending();

            IsVisible = false;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            if (_panel != null)
            {
                _panel.localScale = Vector3.one;
            }
        }

        protected virtual void Awake()
        {
            if (_panel == null)
            {
                _panel = transform as RectTransform;
            }

            if (_primaryButton != null)
            {
                _primaryButton.onClick.AddListener(OnPrimaryClicked);
            }

            ApplyStaticText();
            HideImmediate();
        }

        protected virtual void OnDestroy()
        {
            if (_primaryButton != null)
            {
                _primaryButton.onClick.RemoveListener(OnPrimaryClicked);
            }

            KillTweens();
            CompletePending();
            IsVisible = false;
        }

        private void ApplyStaticText()
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = Title;
                _titleLabel.raycastTarget = false;
            }

            if (_primaryButtonLabel != null)
            {
                _primaryButtonLabel.text = PrimaryButtonText;
                _primaryButtonLabel.raycastTarget = false;
            }
        }

        private void ApplyOpenedState()
        {
            KillTweens();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            if (_panel != null)
            {
                _panel.localScale = Vector3.one;
            }
        }

        private void KillTweens()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.DOKill();
            }

            if (_panel != null)
            {
                _panel.DOKill();
            }
        }

        /// <summary>Releases an awaiter left behind by a cancelled open (I5: no dead continuations).</summary>
        private void CompletePending()
        {
            if (_primaryPressed == null)
            {
                return;
            }

            _primaryPressed.TrySetCanceled();
            _primaryPressed = null;
        }

        private void OnPrimaryClicked()
        {
            if (_primaryPressed != null)
            {
                _primaryPressed.TrySetResult();
            }
        }
    }
}
