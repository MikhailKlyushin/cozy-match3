using System;
using System.Globalization;
using Match3.Content;
using Match3.Core;
using Match3.Progression;
using Match3.Resolve;
using R3;
using UnityEngine;

namespace Match3.Cheats
{
    /// <summary>
    /// Drives the §12 cheat panel. Every board-changing element goes through the attempt's turn
    /// runner, which executes it with <c>TurnRule.ExecuteCheat</c> and replays the transcript like
    /// an ordinary turn (A10); «Перейти» and the seed buttons rebuild the attempt through
    /// <c>LevelFlowRule</c> instead, because they do not mutate a board - they replace it (§15).
    /// Everything here is driven by on-screen widgets: there is not one hot key.
    /// </summary>
    public sealed class CheatPanelPresenter : IDisposable
    {
        private readonly CheatPanelView _view;
        private readonly CheatGridOverlayView _overlay;
        private readonly CheatBoardTapCatcher _tapCatcher;
        private readonly LevelFlowRule _flow;
        private readonly ChipVisualProfile _visuals;
        private readonly IMatch3Logger _logger;

        private readonly ReactiveProperty<bool> _isOpen = new ReactiveProperty<bool>(false);

        private CompositeDisposable _subscriptions;
        private CheatLevelBinding _binding;
        private BoosterType _armedBooster = BoosterType.None;
        private bool _freeMoves;
        private bool _hintsDisabled;
        private bool _showCoordinates;
        private bool _disposed;

        /// <summary>§12: the button reads «+5 ходов», so the amount is not a tuning knob.</summary>
        public const int AddMovesAmount = 5;

        public CheatPanelPresenter(
            CheatsRootView root,
            LevelFlowRule flow,
            ChipVisualProfile visuals,
            IMatch3Logger logger)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (root.Panel == null || root.GridOverlay == null || root.TapCatcher == null)
            {
                throw new ArgumentException(
                    "The cheats prefab must carry a panel, a coordinate overlay and a board tap catcher.",
                    nameof(root));
            }

            _view = root.Panel;
            _overlay = root.GridOverlay;
            _tapCatcher = root.TapCatcher;
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _visuals = visuals != null ? visuals : throw new ArgumentNullException(nameof(visuals));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            SubscribeView();

            _view.SetVisible(false);
            _view.SetFreeMoves(false);
            _view.SetCoordinateGrid(false);
            _view.SetDisableHints(false);
            ApplyArmedBooster();
            RefreshSeedDisplay();
            RefreshInteractable();
        }

        /// <summary>
        /// True while the panel owns the screen. The level's input state reads it, because §5.5
        /// keeps the idle hint and board input parked while the panel is open.
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

        public bool HasAttempt => _binding != null;

        /// <summary>Which booster the next tap on a cell places; <c>None</c> when disarmed.</summary>
        public BoosterType ArmedBooster => _armedBooster;

        public void Open()
        {
            if (_disposed || _isOpen.Value)
            {
                return;
            }

            _isOpen.Value = true;
            _view.SetVisible(true);
            ApplyHintSuspension();
            ApplyArmedBooster();
            RefreshSeedDisplay();
            RefreshInteractable();
        }

        public void Close()
        {
            if (_disposed || !_isOpen.Value)
            {
                return;
            }

            _isOpen.Value = false;
            _view.SetVisible(false);

            // A closed panel must not keep a full-screen tap catcher over the board.
            DisarmBooster();
            ApplyHintSuspension();
        }

        /// <summary>What the HUD bug button calls (§11.2, §12); the composition root wires it.</summary>
        public void Toggle()
        {
            if (_isOpen.Value)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// Binds one level attempt. The panel lives in the scene and outlives attempts, so the
        /// per-attempt objects arrive here exactly as they do for the HUD presenters.
        /// </summary>
        public void Bind(CheatLevelBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            Unbind();

            _binding = binding;
            _subscriptions = new CompositeDisposable();
            _subscriptions.Add(binding.Session.InputBlocked.Subscribe(OnInputBlockedChanged));

            ApplyFreeMovesToAttempt();
            ApplyHintSuspension();
            ApplyCoordinateOverlay();
            ApplyArmedBooster();
            RefreshSeedDisplay();
            RefreshInteractable();
        }

        public void Unbind()
        {
            if (_subscriptions != null)
            {
                _subscriptions.Dispose();
                _subscriptions = null;
            }

            _binding = null;

            DisarmBooster();

            if (_overlay != null)
            {
                _overlay.Hide();
            }

            RefreshSeedDisplay();
            RefreshInteractable();
        }

        /// <summary>
        /// Sends one command to its executor (§15). Board commands reach the attempt's turn runner,
        /// the two that rebuild the attempt reach the level flow, and nothing else is executed.
        /// </summary>
        public void Execute(CheatCommand command)
        {
            if (_disposed)
            {
                return;
            }

            if (CheatCommandRouting.ClosesPanel(command))
            {
                // A WIN/LOSE sequence, its popup or a fresh level owns the screen next.
                Close();
            }

            switch (CheatCommandRouting.Route(command))
            {
                case CheatRoute.Board:
                    ExecuteBoardCommand(command);
                    return;

                case CheatRoute.LevelFlow:
                    ExecuteFlowCommand(command);
                    return;

                default:
                    _logger.Warn("Cheat command " + command.Kind.ToString() + " has no executor.");
                    return;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            UnsubscribeView();
            Unbind();

            if (_view != null)
            {
                _view.SetVisible(false);
            }

            _isOpen.Value = false;
            _isOpen.Dispose();
        }

        /// <summary>A cheat needs an attempt that is idle and still accepting input (§5.1).</summary>
        private bool CanRunBoardCheat
            => _binding != null && _binding.Runner.CanRun && _binding.TurnRule.CanAcceptInput;

        private void ExecuteBoardCommand(in CheatCommand command)
        {
            if (!CanRunBoardCheat)
            {
                return;
            }

            _binding.Runner.ExecuteCheat(command);
            RefreshInteractable();
        }

        private void ExecuteFlowCommand(in CheatCommand command)
        {
            switch (command.Kind)
            {
                case CheatCommandKind.GoToLevel:
                    if (!_flow.CanGoToLevel(command.IntValue))
                    {
                        _logger.Warn("Cheat: level " + command.IntValue.ToString(CultureInfo.InvariantCulture)
                                     + " is not in the catalog.");
                        return;
                    }

                    _flow.GoToLevel(command.IntValue);
                    return;

                case CheatCommandKind.SetSeed:
                    // Same level, explicit seed: the attempt replays bit for bit (D12).
                    _flow.ReplayWithSeed(command.IntValue);
                    return;

                default:
                    return;
            }
        }

        /// <summary>A turn starting or ending changes what the panel may offer.</summary>
        private void OnInputBlockedChanged(bool blocked) => RefreshInteractable();

        private void OnCloseClicked() => Close();

        private void OnLevelNumberChanged(string text) => RefreshLevelStatus(text);

        private void OnGoToLevelClicked()
        {
            if (!CheatLevelInput.TryParseLevelNumber(_view.LevelText, out int levelId))
            {
                return;
            }

            Execute(CheatCommand.GoToLevel(levelId));
        }

        private void OnWinLevelClicked() => Execute(CheatCommand.WinLevel());

        private void OnLoseLevelClicked() => Execute(CheatCommand.LoseLevel());

        private void OnAddMovesClicked() => Execute(CheatCommand.AddMoves(AddMovesAmount));

        private void OnBoosterSelected(BoosterType booster)
        {
            // Pressing the armed booster again disarms it, so the tap catcher is never stuck on.
            _armedBooster = _armedBooster == booster ? BoosterType.None : booster;
            ApplyArmedBooster();
        }

        private void OnDisarmBoosterClicked() => DisarmBooster();

        private void OnBoardTapped(Vector2 screenPosition)
        {
            if (_binding == null || _armedBooster == BoosterType.None)
            {
                return;
            }

            GridPos cell = _binding.Picker.PickCell(screenPosition);
            if (!cell.IsValid)
            {
                return;
            }

            // §12: placing a booster does not spend a move - TurnRule charges nothing for it.
            Execute(CheatCommand.PlaceBooster(cell, _armedBooster));
        }

        private void OnApplySeedClicked()
        {
            if (!CheatLevelInput.TryParseSeed(_view.SeedText, out int seed))
            {
                return;
            }

            Execute(CheatCommand.SetSeed(seed));
        }

        /// <summary>«Заново»: the seed of the live attempt, so the same game runs again (D12).</summary>
        private void OnRestartAttemptClicked()
        {
            if (_binding == null)
            {
                return;
            }

            Execute(CheatCommand.SetSeed(_binding.Session.Seed));
        }

        private void OnFreeMovesChanged(bool value)
        {
            _freeMoves = value;
            Execute(CheatCommand.FreeMoves(value));
        }

        private void OnCoordinateGridChanged(bool value)
        {
            _showCoordinates = value;
            ApplyCoordinateOverlay();
        }

        private void OnDisableHintsChanged(bool value)
        {
            _hintsDisabled = value;
            ApplyHintSuspension();
        }

        private void OnHintNowClicked()
        {
            if (_binding == null)
            {
                return;
            }

            _binding.Hints.ShowNow();
        }

        private void OnDumpTranscriptClicked()
        {
            TurnTranscript transcript = _binding != null ? _binding.Runner.LastTranscript : null;
            if (transcript == null)
            {
                _logger.Warn("Cheat: no turn has been recorded on this attempt yet.");
                return;
            }

            _logger.Info(CheatTranscriptDump.Format(transcript));
        }

        private void DisarmBooster()
        {
            _armedBooster = BoosterType.None;
            ApplyArmedBooster();
        }

        private void ApplyArmedBooster()
        {
            bool armed = _armedBooster != BoosterType.None;

            if (_view != null)
            {
                _view.SetArmedBooster(_armedBooster, armed ? _visuals.GetBoosterSprite(_armedBooster) : null);
            }

            if (_tapCatcher != null)
            {
                // Only while the panel is open: with it closed the board's own input owns taps.
                _tapCatcher.SetArmed(armed && _isOpen.Value && _binding != null);
            }
        }

        private void ApplyCoordinateOverlay()
        {
            if (_overlay == null)
            {
                return;
            }

            if (_showCoordinates && _binding != null)
            {
                _overlay.Show(_binding.Picker);
                return;
            }

            _overlay.Hide();
        }

        /// <summary>§5.5: the idle timer is silent while the panel is open, and while hints are off.</summary>
        private void ApplyHintSuspension()
        {
            if (_binding == null)
            {
                return;
            }

            _binding.Hints.SetSuspended(_hintsDisabled || _isOpen.Value);
        }

        /// <summary>
        /// A new attempt builds a new TurnRule with the flag cleared, so the toggle is re-applied.
        /// It still goes through ExecuteCheat (A10), but not through the turn runner: the command
        /// produces no visual event and the attempt is still being built.
        /// </summary>
        private void ApplyFreeMovesToAttempt()
        {
            if (!_freeMoves || _binding == null)
            {
                return;
            }

            _binding.TurnRule.ExecuteCheat(CheatCommand.FreeMoves(true));
        }

        private void RefreshSeedDisplay()
        {
            if (_disposed || _view == null)
            {
                return;
            }

            if (_binding == null)
            {
                _view.SetSeedDisplay(CheatStrings.SeedUnknown);
                return;
            }

            int seed = _binding.Session.Seed;
            _view.SetSeedDisplay(string.Format(CultureInfo.InvariantCulture, CheatStrings.SeedFormat, seed));
            _view.SetSeedText(seed.ToString(CultureInfo.InvariantCulture));
        }

        private void RefreshInteractable()
        {
            if (_disposed || _view == null)
            {
                return;
            }

            bool idle = _binding != null && _binding.Runner.CanRun;

            _view.SetBoardCheatsInteractable(idle && _binding.TurnRule.CanAcceptInput);
            _view.SetSeedActionsInteractable(idle && CheatLevelInput.TryParseSeed(_view.SeedText, out _), idle);
            _view.SetCoordinateGridInteractable(_binding != null);
            _view.SetDumpInteractable(_binding != null && _binding.Runner.LastTranscript != null);
            RefreshLevelStatus(_view.LevelText);
        }

        /// <summary>
        /// «Перейти» is disabled for a number the catalog does not hold. The catalog is asked, not
        /// a hardcoded range: level ids are data and need not be contiguous (§8.3).
        /// </summary>
        private void RefreshLevelStatus(string text)
        {
            if (!CheatLevelInput.TryParseLevelNumber(text, out int levelId))
            {
                _view.SetLevelStatus(CheatStrings.LevelEmpty);
                _view.SetGoToLevelInteractable(false);
                return;
            }

            bool exists = _flow.CanGoToLevel(levelId);
            _view.SetLevelStatus(string.Format(
                CultureInfo.InvariantCulture,
                exists ? CheatStrings.LevelReadyFormat : CheatStrings.LevelMissingFormat,
                levelId));
            _view.SetGoToLevelInteractable(exists);
        }

        private void SubscribeView()
        {
            _view.CloseClicked += OnCloseClicked;
            _view.LevelNumberChanged += OnLevelNumberChanged;
            _view.GoToLevelClicked += OnGoToLevelClicked;
            _view.WinLevelClicked += OnWinLevelClicked;
            _view.LoseLevelClicked += OnLoseLevelClicked;
            _view.AddMovesClicked += OnAddMovesClicked;
            _view.BoosterSelected += OnBoosterSelected;
            _view.DisarmBoosterClicked += OnDisarmBoosterClicked;
            _view.ApplySeedClicked += OnApplySeedClicked;
            _view.RestartAttemptClicked += OnRestartAttemptClicked;
            _view.FreeMovesChanged += OnFreeMovesChanged;
            _view.CoordinateGridChanged += OnCoordinateGridChanged;
            _view.DisableHintsChanged += OnDisableHintsChanged;
            _view.HintNowClicked += OnHintNowClicked;
            _view.DumpTranscriptClicked += OnDumpTranscriptClicked;
            _tapCatcher.Tapped += OnBoardTapped;
        }

        private void UnsubscribeView()
        {
            if (_view != null)
            {
                _view.CloseClicked -= OnCloseClicked;
                _view.LevelNumberChanged -= OnLevelNumberChanged;
                _view.GoToLevelClicked -= OnGoToLevelClicked;
                _view.WinLevelClicked -= OnWinLevelClicked;
                _view.LoseLevelClicked -= OnLoseLevelClicked;
                _view.AddMovesClicked -= OnAddMovesClicked;
                _view.BoosterSelected -= OnBoosterSelected;
                _view.DisarmBoosterClicked -= OnDisarmBoosterClicked;
                _view.ApplySeedClicked -= OnApplySeedClicked;
                _view.RestartAttemptClicked -= OnRestartAttemptClicked;
                _view.FreeMovesChanged -= OnFreeMovesChanged;
                _view.CoordinateGridChanged -= OnCoordinateGridChanged;
                _view.DisableHintsChanged -= OnDisableHintsChanged;
                _view.HintNowClicked -= OnHintNowClicked;
                _view.DumpTranscriptClicked -= OnDumpTranscriptClicked;
            }

            if (_tapCatcher != null)
            {
                _tapCatcher.Tapped -= OnBoardTapped;
            }
        }
    }
}
