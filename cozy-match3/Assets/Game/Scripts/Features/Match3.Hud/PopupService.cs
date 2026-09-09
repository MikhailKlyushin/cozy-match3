using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Core;
using Match3.Goals;

namespace Match3.Hud
{
    /// <summary>
    /// Opens one of the registered popups and completes when its action button is pressed. Popups
    /// are pre-placed views, looked up by kind the way event players are (A11), so nothing is
    /// instantiated at runtime.
    /// </summary>
    public sealed class PopupService
    {
        private readonly PopupView[] _byKind;
        private readonly GoalIconResolver _icons;
        private readonly IMatch3Logger _logger;

        private const int KindCount = (int)PopupKind.EndOfContent + 1;

        public PopupService(IReadOnlyList<PopupView> popups, GoalIconResolver icons, IMatch3Logger logger)
        {
            if (popups == null)
            {
                throw new ArgumentNullException(nameof(popups));
            }

            _icons = icons ?? throw new ArgumentNullException(nameof(icons));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _byKind = new PopupView[KindCount];

            for (int i = 0; i < popups.Count; i++)
            {
                PopupView popup = popups[i];
                if (popup == null)
                {
                    continue;
                }

                int index = (int)popup.Kind;
                if (_byKind[index] != null)
                {
                    throw new InvalidOperationException("Two popups registered for kind " + popup.Kind);
                }

                _byKind[index] = popup;
            }
        }

        /// <summary>True while a popup is on screen; the hint timer stays parked meanwhile (§5.5).</summary>
        public bool IsOpen { get; private set; }

        public UniTask ShowWinAsync(IGoalTracker goals, CancellationToken ct)
            => ShowAsync(PopupKind.Win, PopupContent.ForGoals(goals), ct);

        public UniTask ShowLoseAsync(IGoalTracker goals, CancellationToken ct)
            => ShowAsync(PopupKind.Lose, PopupContent.ForGoals(goals), ct);

        public UniTask ShowEndOfContentAsync(int lastLevelId, CancellationToken ct)
            => ShowAsync(PopupKind.EndOfContent, PopupContent.ForEndOfContent(lastLevelId), ct);

        /// <summary>Completes once the player presses the popup's action button.</summary>
        public async UniTask ShowAsync(PopupKind kind, PopupContent content, CancellationToken ct)
        {
            PopupView popup = Resolve(kind);

            IsOpen = true;
            try
            {
                popup.Present(content, _icons);
                await popup.ShowAsync(ct);
                await popup.WaitForPrimaryAsync(ct);
                await popup.HideAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Torn down while open: leave nothing half-faded on screen.
                popup.HideImmediate();
                throw;
            }
            catch (Exception e)
            {
                popup.HideImmediate();
                _logger.Error("Popup " + kind + " failed: " + e);
                throw;
            }
            finally
            {
                IsOpen = false;
            }
        }

        public void CloseAll()
        {
            for (int i = 0; i < _byKind.Length; i++)
            {
                PopupView popup = _byKind[i];
                if (popup != null)
                {
                    popup.HideImmediate();
                }
            }

            IsOpen = false;
        }

        private PopupView Resolve(PopupKind kind)
        {
            int index = (int)kind;
            if ((uint)index >= (uint)_byKind.Length || _byKind[index] == null)
            {
                throw new InvalidOperationException("No popup registered for kind " + kind);
            }

            return _byKind[index];
        }
    }
}
