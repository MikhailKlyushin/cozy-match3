using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Match3.Content;
using Match3.Core;
using Match3.Resolve;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Match3.Gameplay
{
    /// <summary>
    /// The idle hint on screen (§11.3): both swap cells pulse, a direction arrow fades in, and
    /// after a pause the whole show repeats until input arrives. A tap hint marks one cell and
    /// draws no arrow.
    /// </summary>
    public sealed class HintView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _firstMarker;
        [SerializeField] private Image _secondMarker;
        [SerializeField] private Image _arrow;

        private IBoardCellPicker _picker;
        private TimingProfile _timings;
        private bool _visible;

        /// <summary>One §11.3 pulse cycle is two yoyo legs: scale up, then back.</summary>
        private const float HalfCycle = 0.5f;

        private const int YoyoLoopsPerCycle = 2;

        private const float Midpoint = 0.5f;

        [Inject]
        public void Construct(IBoardCellPicker picker, TimingProfile timings)
        {
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _timings = timings != null ? timings : throw new ArgumentNullException(nameof(timings));

            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            // The hint must never eat a tap meant for the board (§11.3).
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _firstMarker.raycastTarget = false;
            _secondMarker.raycastTarget = false;
            _arrow.raycastTarget = false;
        }

        /// <summary>
        /// Pulses, pauses <see cref="TimingProfile.HintRepeatPause"/> and repeats until the token
        /// is cancelled - the player may have looked away, so one show is not enough (§5.5).
        /// </summary>
        public async UniTask ShowAsync(HintPlan plan, CancellationToken ct)
        {
            if (!plan.HasHint)
            {
                return;
            }

            Place(plan);
            _group.alpha = 1f;
            _visible = true;

            while (!ct.IsCancellationRequested)
            {
                await PulseAsync(plan, ct);
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_timings.HintRepeatPause),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);
            }

            ct.ThrowIfCancellationRequested();
        }

        /// <summary>Fades out over <see cref="TimingProfile.HintHide"/> on any input (§11.3).</summary>
        public async UniTask HideAsync(CancellationToken ct)
        {
            if (!_visible)
            {
                return;
            }

            _visible = false;
            KillTweens();
            _group.DOFade(0f, _timings.HintHide).SetLink(gameObject);

            await UniTask.Delay(
                TimeSpan.FromSeconds(_timings.HintHide),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);

            _group.alpha = 0f;
        }

        /// <summary>No fade: used when the level attempt is torn down mid-show.</summary>
        public void HideImmediate()
        {
            _visible = false;
            KillTweens();
            _group.alpha = 0f;
        }

        public void KillTweens()
        {
            _group.DOKill();
            _firstMarker.DOKill();
            _firstMarker.rectTransform.DOKill();
            _secondMarker.DOKill();
            _secondMarker.rectTransform.DOKill();
            _arrow.DOKill();
            _arrow.rectTransform.DOKill();
        }

        private void OnDestroy() => KillTweens();

        private async UniTask PulseAsync(HintPlan plan, CancellationToken ct)
        {
            float cycle = _timings.HintPulseCycle;
            int cycles = _timings.HintPulseCycles;

            Pulse(_firstMarker.rectTransform, cycle, cycles);

            if (plan.Kind == HintKind.Swap)
            {
                Pulse(_secondMarker.rectTransform, cycle, cycles);

                Color color = _arrow.color;
                color.a = 0f;
                _arrow.color = color;
                _arrow.DOFade(1f, _timings.HintArrowFadeIn).SetLink(gameObject);
            }

            await UniTask.Delay(
                TimeSpan.FromSeconds(cycle * cycles),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);
        }

        private void Pulse(RectTransform marker, float cycle, int cycles)
        {
            marker.DOKill();
            marker.localScale = Vector3.one;
            marker.DOScale(_timings.HintPulseScale, cycle * HalfCycle)
                .SetLoops(cycles * YoyoLoopsPerCycle, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(gameObject);
        }

        private void Place(HintPlan plan)
        {
            float cellSize = CellLocalSize();
            bool isSwap = plan.Kind == HintKind.Swap;

            PlaceMarker(_firstMarker, plan.A, cellSize);

            _secondMarker.enabled = isSwap;
            _arrow.enabled = isSwap;

            if (!isSwap)
            {
                return;
            }

            PlaceMarker(_secondMarker, plan.B, cellSize);
            PlaceArrow(plan.A, plan.B, cellSize);
        }

        private void PlaceMarker(Image marker, GridPos cell, float cellSize)
        {
            marker.enabled = true;
            RectTransform rect = marker.rectTransform;
            rect.anchoredPosition = ToLocal(_picker.CellToScreen(cell));
            rect.localScale = Vector3.one;

            if (cellSize > 0f)
            {
                rect.sizeDelta = new Vector2(cellSize, cellSize);
            }
        }

        /// <summary>Arrow sits between the two cells and points from A to B (§11.3).</summary>
        private void PlaceArrow(GridPos a, GridPos b, float cellSize)
        {
            Vector2 from = ToLocal(_picker.CellToScreen(a));
            Vector2 to = ToLocal(_picker.CellToScreen(b));
            Vector2 direction = to - from;

            RectTransform rect = _arrow.rectTransform;
            rect.anchoredPosition = from + direction * Midpoint;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            if (cellSize > 0f)
            {
                rect.sizeDelta = new Vector2(cellSize, cellSize);
            }
        }

        /// <summary>
        /// Cell size in this rect's own units, measured between two neighbours: a hardcoded pixel
        /// size breaks outside the reference aspect (§11.3).
        /// </summary>
        private float CellLocalSize()
        {
            var origin = new GridPos(0, 0);

            if (_picker.BoardWidth > 1)
            {
                return Vector2.Distance(
                    ToLocal(_picker.CellToScreen(origin)),
                    ToLocal(_picker.CellToScreen(new GridPos(1, 0))));
            }

            if (_picker.BoardHeight > 1)
            {
                return Vector2.Distance(
                    ToLocal(_picker.CellToScreen(origin)),
                    ToLocal(_picker.CellToScreen(new GridPos(0, 1))));
            }

            return 0f;
        }

        private Vector2 ToLocal(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPosition, null, out Vector2 local);
            return local;
        }
    }
}
