using System;
using Match3.Board;
using Match3.Boosters;
using Match3.Core;
using Match3.Goals;
using Match3.Matching;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// The only entry point that mutates the board, cheats included (A10), and it always returns
    /// a transcript. The full turn is IDLE, VALIDATE, COMMIT, RESOLVE, POST-TURN (§5.1).
    /// </summary>
    public sealed class TurnRule
    {
        private readonly BoardModel _board;
        private readonly LevelRules _rules;
        private readonly SwapValidator _validator;
        private readonly LegalMoveService _legalMoves;
        private readonly ComboResolver _combos;
        private readonly ActivationService _activation;
        private readonly ResolveLoopService _loop;
        private readonly IGoalTracker _goals;
        private readonly ShuffleService _shuffle;
        private readonly MovesBonusService _bonus;
        private readonly HintService _hint;
        private readonly IRandom _random;
        private readonly IMatch3Logger _logger;
        private readonly TurnTranscript _transcript;
        private readonly TranscriptWriter _writer;
        private readonly ResolveContext _context;

        private readonly CellBuffer _bonusCells = new CellBuffer();

        private bool _finished;

        public TurnRule(
            BoardModel board,
            LevelRules rules,
            SwapValidator validator,
            LegalMoveService legalMoves,
            ComboResolver combos,
            ActivationService activation,
            ResolveLoopService loop,
            IGoalTracker goals,
            ShuffleService shuffle,
            MovesBonusService bonus,
            HintService hint,
            IRandom random,
            IMatch3Logger logger,
            TurnTranscript transcript,
            ResolveContext context)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _legalMoves = legalMoves ?? throw new ArgumentNullException(nameof(legalMoves));
            _combos = combos ?? throw new ArgumentNullException(nameof(combos));
            _activation = activation ?? throw new ArgumentNullException(nameof(activation));
            _loop = loop ?? throw new ArgumentNullException(nameof(loop));
            _goals = goals ?? throw new ArgumentNullException(nameof(goals));
            _shuffle = shuffle ?? throw new ArgumentNullException(nameof(shuffle));
            _bonus = bonus ?? throw new ArgumentNullException(nameof(bonus));
            _hint = hint ?? throw new ArgumentNullException(nameof(hint));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _writer = new TranscriptWriter(_transcript);
            _context.Configure(board.Width, board.Height);
            _bonusCells.Configure(board.Width, board.Height);

            MovesLeft = rules.MoveLimit;
        }

        public int MovesLeft { get; private set; }

        /// <summary>Cheat toggle (§12): the counter stops decreasing.</summary>
        public bool FreeMoves { get; set; }

        /// <summary>False once the level ended; the flow layer then tears the attempt down.</summary>
        public bool CanAcceptInput => !_finished;

        public TurnTranscript ExecuteSwap(GridPos a, GridPos b)
        {
            BeginTurn();

            // VALIDATE (§5.1): an illegal swap bounces and charges nothing (E16).
            if (!_validator.IsLegal(a, b))
            {
                _writer.SwapRejected(a, b);
                _writer.SetOutcome(TurnOutcome.Rejected);
                _writer.TurnEnd();
                return _transcript;
            }

            // COMMIT: the move is charged here, before any resolution (D04, E15).
            _board.SwapSlots(a, b);
            _legalMoves.Invalidate();
            _writer.SwapPerformed(a, b);
            ChargeMove();

            ChipSlot atTarget = _board.GetSlot(b);
            ChipSlot atSource = _board.GetSlot(a);

            ComboPlan plan = null;
            GridPos comboSource = GridPos.Invalid;

            if (atTarget.Kind == SlotKind.Booster && atSource.Kind == SlotKind.Booster)
            {
                // Booster plus booster: the epicentre is the swap TARGET, where the second
                // booster lay before the swap (§6.1).
                plan = _combos.Resolve(b, atTarget.Booster, a, atSource.Booster);
                comboSource = a;
            }
            else if (atTarget.Kind == SlotKind.Booster)
            {
                // The booster activates in its NEW cell; a rainbow takes the colour of the chip
                // it traded places with (§6.1, E06).
                _activation.QueuePlayerActivation(_context, b, ColorOf(atSource));
            }
            else if (atSource.Kind == SlotKind.Booster)
            {
                _activation.QueuePlayerActivation(_context, a, ColorOf(atTarget));
            }

            return Resolve(b, plan, comboSource, BoosterActivationSource.Player);
        }

        /// <summary>Tap on a booster: costs one move and fires in its own cell (D03).</summary>
        public TurnTranscript ExecuteTap(GridPos cell)
        {
            BeginTurn();

            if (!_board.Contains(cell) || _board.GetSlot(cell).Kind != SlotKind.Booster)
            {
                _writer.SwapRejected(cell, cell);
                _writer.SetOutcome(TurnOutcome.Rejected);
                _writer.TurnEnd();
                return _transcript;
            }

            _writer.SwapPerformed(cell, cell);
            ChargeMove();

            // ColorHint None makes a tapped rainbow use the most needed colour (§6.1).
            _activation.QueuePlayerActivation(_context, cell, ChipColor.None);

            return Resolve(GridPos.Invalid, null, GridPos.Invalid, BoosterActivationSource.Player);
        }

        public TurnTranscript ExecuteCheat(in CheatCommand command)
        {
            BeginTurn();

            switch (command.Kind)
            {
                case CheatCommandKind.AddMoves:
                    MovesLeft += command.IntValue;
                    _writer.MoveCharged(MovesLeft);
                    _writer.SetOutcome(TurnOutcome.Resolved);
                    _writer.TurnEnd();
                    return _transcript;

                case CheatCommandKind.FreeMoves:
                    FreeMoves = command.BoolValue;
                    _writer.SetOutcome(TurnOutcome.Resolved);
                    _writer.TurnEnd();
                    return _transcript;

                case CheatCommandKind.PlaceBooster:
                    return PlaceBoosterCheat(command);

                case CheatCommandKind.WinLevel:
                    return WinLevelCheat();

                case CheatCommandKind.LoseLevel:
                    MovesLeft = 0;
                    _writer.MoveCharged(0);
                    Finish(TurnOutcome.Lost, LevelEndReason.Cheat);
                    return _transcript;

                default:
                    // GoToLevel and SetSeed recreate the attempt instead of touching the board,
                    // so the cheat presenter routes them to the level flow (§15).
                    _writer.SetOutcome(TurnOutcome.Rejected);
                    _writer.TurnEnd();
                    return _transcript;
            }
        }

        /// <summary>Deterministic, RNG-free suggestion (§5.5, D15).</summary>
        public HintPlan GetHint() => _finished ? HintPlan.None : _hint.GetHint();

        private static ChipColor ColorOf(in ChipSlot slot)
            => slot.Kind == SlotKind.Chip ? slot.Color : ChipColor.None;

        private void BeginTurn()
        {
            _writer.TurnBegin(_random.Seed, MovesLeft);
            _context.BeginTurn();
        }

        private void ChargeMove()
        {
            if (!FreeMoves && MovesLeft > 0)
            {
                MovesLeft--;
            }

            _writer.MoveCharged(MovesLeft);
        }

        private TurnTranscript Resolve(
            GridPos playerSwapTarget,
            ComboPlan plan,
            GridPos comboSource,
            BoosterActivationSource firstWaveSource)
        {
            _loop.Run(_context, playerSwapTarget, plan, comboSource, firstWaveSource, _writer);
            _legalMoves.Invalidate();
            RunPostTurn();
            return _transcript;
        }

        /// <summary>
        /// POST-TURN in the fixed §5.1 order: cx recolours, then goals, then moves, then the
        /// legal-move check. Win beats lose when both hold (D14, E13, E14).
        /// </summary>
        private void RunPostTurn()
        {
            _writer.PostTurnBegin();

            CycleElementColors();

            if (_goals.AllClosed)
            {
                RunMovesBonus();
                Finish(TurnOutcome.Won, LevelEndReason.GoalsClosed);
                return;
            }

            if (MovesLeft <= 0)
            {
                Finish(TurnOutcome.Lost, LevelEndReason.MovesExhausted);
                return;
            }

            if (!_legalMoves.HasAnyMove && !_shuffle.Run(_writer))
            {
                // Shuffle could not restore a move: end the level and log it, because a deadlock
                // is always a bug rather than balance (E19).
                Finish(
                    _goals.AllClosed ? TurnOutcome.Won : TurnOutcome.Lost,
                    _goals.AllClosed ? LevelEndReason.GoalsClosed : LevelEndReason.Deadlock);
                return;
            }

            _writer.SetOutcome(TurnOutcome.Resolved);
            _writer.TurnEnd();
        }

        /// <summary>
        /// cx changes colour every turn regardless of damage, and never mid-resolution: damage
        /// used the colour as of the DAMAGE stage (§7.2).
        /// </summary>
        private void CycleElementColors()
        {
            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (!_board.TryGetElement(cell, out ElementInstance element) || !element.IsAlive)
                    {
                        continue;
                    }

                    ElementDefinition definition = _board.Catalog.Get(element.Definition);
                    if (definition.PerTurn != PerTurnBehaviour.CycleColor)
                    {
                        continue;
                    }

                    element.CurrentColor = ChipColors.NextCycling(element.CurrentColor, _rules.ColorCount);
                    _writer.ElementColorCycled(cell, element.CurrentColor);
                }
            }
        }

        /// <summary>Each leftover move becomes a rocket and fires; it cannot lose the level (E23).</summary>
        private void RunMovesBonus()
        {
            _bonusCells.Clear();
            int picked = _bonus.PickCells(MovesLeft, _bonusCells);

            for (int i = 0; i < picked; i++)
            {
                GridPos cell = _bonusCells[i];
                BoosterType rocket = (i & 1) == 0 ? BoosterType.RocketH : BoosterType.RocketV;

                int instanceId = _board.SetBooster(cell, rocket);
                _writer.MovesBonusRocket(cell, rocket, instanceId);

                _context.BeginTurn();
                _activation.QueuePlayerActivation(_context, cell, ChipColor.None);
                _loop.Run(_context, GridPos.Invalid, null, GridPos.Invalid, BoosterActivationSource.Bonus, _writer);
            }

            MovesLeft = 0;
            _legalMoves.Invalidate();
        }

        private TurnTranscript PlaceBoosterCheat(in CheatCommand command)
        {
            if (!_board.Contains(command.Cell) || _board.GetKind(command.Cell) != CellKind.Playable)
            {
                _writer.SetOutcome(TurnOutcome.Rejected);
                _writer.TurnEnd();
                return _transcript;
            }

            // §12: placing a booster does not spend a move.
            int instanceId = _board.SetBooster(command.Cell, command.Booster);
            _legalMoves.Invalidate();
            _writer.BoosterSpawned(command.Cell, command.Booster, instanceId);
            _writer.SetOutcome(TurnOutcome.Resolved);
            _writer.TurnEnd();
            return _transcript;
        }

        /// <summary>Closes every goal and plays the win sequence, leftover-move bonus included (§12).</summary>
        private TurnTranscript WinLevelCheat()
        {
            int from = _context.GoalDeltas.Count;
            _goals.CloseAll(_context.GoalDeltas);
            GoalProgressWriter.Write(_context.GoalDeltas, from, _writer);

            _writer.PostTurnBegin();
            RunMovesBonus();
            Finish(TurnOutcome.Won, LevelEndReason.Cheat);
            return _transcript;
        }

        private void Finish(TurnOutcome outcome, LevelEndReason reason)
        {
            _finished = true;

            if (reason == LevelEndReason.Deadlock)
            {
                _logger.Error("Level ended in deadlock on level " + _rules.LevelId.ToString() + " (E19)");
            }

            if (outcome == TurnOutcome.Won)
            {
                _writer.LevelWon(reason);
            }
            else
            {
                _writer.LevelLost(reason);
            }

            _writer.TurnEnd();
        }
    }
}
