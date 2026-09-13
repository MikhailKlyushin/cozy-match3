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

            UnbindCurrent();

            _binding = binding;

            ApplyFreeMovesToAttempt();
            ApplyHintSuspension();
            ApplyCoordinateOverlay();
            ApplyArmedBooster();
            RefreshSeedDisplay();
            RefreshInteractable();
        }

        /// <summary>Releases whatever attempt is bound. The panel's own teardown path.</summary>
        public void Unbind() => UnbindCurrent();

        /// <summary>
        /// Releases one particular attempt, and only that one. Zenject initialises a dynamically
        /// created GameObjectContext immediately, while Unity destroys the previous one at the end
        /// of the frame, so the dying attempt disposes after its successor has already bound
        /// itself. An unconditional unbind there left the panel holding no attempt at all for the
        /// rest of the session, which is what greyed out every board cheat after the first
        /// restart, replay or level change. An attempt that never bound (null) releases nothing.
        /// </summary>
        public void Unbind(CheatLevelBinding binding)
        {
            if (!ReferenceEquals(_binding, binding))
            {
                return;
            }

            UnbindCurrent();
        }

        /// <summary>
        /// Sends one command to its executor (§15). Board commands reach the attempt's turn runner,
        /// the two that rebuild the attempt reach the level flow, and nothing else is executed.
        /// Returns whether the command actually ran: a refused command must not close the panel,
        /// now that the controls stay pressable whatever the board is doing.
        /// </summary>
        public bool Execute(CheatCommand command)
        {
            if (_disposed)
            {
                return false;
            }

            bool executed;
            switch (CheatCommandRouting.Route(command))
            {
                case CheatRoute.Board:
                    executed = ExecuteBoardCommand(command);
                    break;

                case CheatRoute.LevelFlow:
                    executed = ExecuteFlowCommand(command);
                    break;

                default:
                    _logger.Warn("Cheat command " + command.Kind.ToString() + " has no executor.");
                    return false;
            }

            if (executed && CheatCommandRouting.ClosesPanel(command))
            {
                // A WIN/LOSE sequence, its popup or a fresh level owns the screen next.
                Close();
            }

            return executed;
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

        /// <summary>
        /// A board cheat still needs an attempt that is idle and unfinished: TurnRule would
        /// otherwise write a second outcome into a level that has already ended (§5.1). The panel
        /// no longer greys its controls out for that - it says in the log why nothing happened,
        /// because a control that disables itself mid-cascade reads as a broken cheat panel.
        /// </summary>
        private bool ExecuteBoardCommand(in CheatCommand command)
        {
            if (_binding == null)
            {
                _logger.Warn("Cheat " + command.Kind.ToString() + " needs a live level attempt.");
                return false;
            }

            if (!_binding.Runner.CanRun || !_binding.TurnRule.CanAcceptInput)
            {
                _logger.Warn("Cheat " + command.Kind.ToString()
                             + " ignored: a turn is still playing or the level has ended.");
                return false;
            }

            _binding.Runner.ExecuteCheat(command);
            RefreshInteractable();
            return true;
        }

        private bool ExecuteFlowCommand(in CheatCommand command)
        {
            switch (command.Kind)
            {
                case CheatCommandKind.GoToLevel:
                    if (!_flow.CanGoToLevel(command.IntValue))
                    {
                        _logger.Warn("Cheat: level " + command.IntValue.ToString(CultureInfo.InvariantCulture)
                                     + " is not in the catalog.");
                        return false;
                    }

                    _flow.GoToLevel(command.IntValue);
                    return true;

                case CheatCommandKind.SetSeed:
                    // Same level, explicit seed: the attempt replays bit for bit (D12).
                    _flow.ReplayWithSeed(command.IntValue);
                    return true;

                default:
                    return false;
            }
        }

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
            if (!Execute(CheatCommand.FreeMoves(value)))
            {
                // The board refused the command, so the switch must not claim the flag is set.
                _view.SetFreeMoves(_freeMoves);
                return;
            }

            _freeMoves = value;
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

        private void UnbindCurrent()
        {
            _binding = null;

            DisarmBooster();

            if (_overlay != null)
            {
                _overlay.Hide();
            }

            RefreshSeedDisplay();
            RefreshInteractable();
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

        /// <summary>
        /// Interactability follows one question: is there an attempt to cheat on. It deliberately
        /// does not follow the turn state - the execution guard already refuses what the board
        /// cannot take, and it explains itself in the log while a grey button does not.
        /// </summary>
        private void RefreshInteractable()
        {
            if (_disposed || _view == null)
            {
                return;
            }

            bool bound = _binding != null;

            _view.SetBoardCheatsInteractable(bound);
            _view.SetSeedActionsInteractable(bound && CheatLevelInput.TryParseSeed(_view.SeedText, out _), bound);
            _view.SetCoordinateGridInteractable(bound);
            _view.SetDumpInteractable(bound);
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
