using System;
using System.Collections.Generic;
using Match3.Board;
using Match3.Core;
using Match3.Matching;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Board shuffle (GDD §5.4), triggered on POST-TURN when no legal move remains. Obstacles and
    /// blockers never move and no new chip is spawned, so the player sees the colour distribution
    /// change rather than a different board.
    /// </summary>
    public sealed class ShuffleService
    {
        private readonly BoardModel _board;
        private readonly MatchDetectionService _detection;
        private readonly LegalMoveService _legalMoves;
        private readonly IChipSpawnPolicy _spawnPolicy;
        private readonly LevelRules _rules;
        private readonly IRandom _random;
        private readonly IMatch3Logger _logger;

        private readonly List<GridPos> _positions = new List<GridPos>(96);
        private readonly List<ChipSlot> _slots = new List<ChipSlot>(96);
        private readonly List<GridPos> _originCells = new List<GridPos>(96);
        private readonly List<int> _originIds = new List<int>(96);
        private readonly List<ChipColor> _originColors = new List<ChipColor>(96);

        public ShuffleService(
            BoardModel board,
            MatchDetectionService detection,
            LegalMoveService legalMoves,
            IChipSpawnPolicy spawnPolicy,
            LevelRules rules,
            IRandom random,
            IMatch3Logger logger)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _detection = detection ?? throw new ArgumentNullException(nameof(detection));
            _legalMoves = legalMoves ?? throw new ArgumentNullException(nameof(legalMoves));
            _spawnPolicy = spawnPolicy ?? throw new ArgumentNullException(nameof(spawnPolicy));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Number of shuffle attempts spent, for the shuffle_triggered metric (§14).</summary>
        public int LastAttemptCount { get; private set; }

        /// <summary>
        /// Returns true when the board ends up with no ready match and at least one legal move.
        /// False means deadlock, which is always a bug rather than balance (E19).
        /// </summary>
        public bool Run(TranscriptWriter writer)
        {
            Collect();

            if (_positions.Count < 3)
            {
                _logger.Error("Shuffle impossible: fewer than three movable chips remain (E19)");
                return false;
            }

            writer.ShuffleBegin();
            LastAttemptCount = 0;

            for (int attempt = 0; attempt < ResolveCaps.MaxShuffleAttempts; attempt++)
            {
                LastAttemptCount++;
                _random.Shuffle(_slots);
                WriteBack();

                if (IsPlayable())
                {
                    EmitMoves(writer);
                    writer.ShuffleEnd();
                    return true;
                }
            }

            // Second phase (§5.4 step 3): regenerate the colours instead of permuting them.
            for (int attempt = 0; attempt < ResolveCaps.MaxShuffleAttempts; attempt++)
            {
                LastAttemptCount++;
                Regenerate();

                if (IsPlayable())
                {
                    EmitRecolours(writer);
                    writer.ShuffleEnd();
                    return true;
                }
            }

            // Third phase (§5.4 step 4): stop re-rolling and build a match-free arrangement by
            // construction, then buy a legal move with one swap. Both earlier phases draw
            // uniformly, so on a fragmented board they can miss an acceptable arrangement in
            // twenty tries - and a deadlock is a bug, not balance (E19).
            LastAttemptCount++;
            RefillWithoutMatches();

            if (IsPlayable() || TryRepair())
            {
                EmitRecolours(writer);
                writer.ShuffleEnd();
                return true;
            }

            writer.ShuffleEnd();
            _logger.Error("Shuffle exhausted all three phases without restoring a legal move (E19)");
            return false;
        }

        private void Collect()
        {
            _positions.Clear();
            _slots.Clear();
            _originCells.Clear();
            _originIds.Clear();
            _originColors.Clear();

            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    ChipSlot slot = _board.GetSlot(cell);

                    // Boosters stay put: they are an accumulated player resource, and §5.4 only
                    // reshuffles chips.
                    if (slot.Kind != SlotKind.Chip || !_board.IsMovable(cell))
                    {
                        continue;
                    }

                    _positions.Add(cell);
                    _slots.Add(slot);
                    _originCells.Add(cell);
                    _originIds.Add(slot.InstanceId);
                    _originColors.Add(slot.Color);
                }
            }
        }

        private void WriteBack()
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                ChipSlot slot = _slots[i];
                _board.SetChip(_positions[i], slot.Color, slot.InstanceId);
            }

            _legalMoves.Invalidate();
        }

        private void Regenerate()
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                GridPos cell = _positions[i];
                ChipColor color = _spawnPolicy.NextColor(cell.X);

                // A fresh id on purpose: the pair below tells the view that the old chip died and
                // a new one arrived, and reusing the id would make those two events fight over
                // one view - the spawn would win the map and the destroy would then remove it.
                _board.SetChip(cell, color);
            }

            _legalMoves.Invalidate();
        }

        /// <summary>
        /// Every cell takes a colour that closes no primitive, so the result has no ready match
        /// whatever the board shape (E20 uses the same rule for the initial board).
        /// </summary>
        private void RefillWithoutMatches()
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                _board.ClearSlot(_positions[i]);
            }

            for (int i = 0; i < _positions.Count; i++)
            {
                GridPos cell = _positions[i];
                _board.SetChip(cell, PickColor(_board, cell, _rules.ColorCount, _random));
            }

            _legalMoves.Invalidate();
        }

        /// <summary>E20: re-roll a colour that would close a match, at most colorCount - 1 times.</summary>
        private static ChipColor PickColor(BoardModel board, GridPos cell, int colorCount, IRandom random)
        {
            ChipColor color = ChipColors.FromIndex(random.NextInt(colorCount) + 1);
            for (int reroll = 0;
                 reroll < colorCount - 1 && MatchFreePlacement.WouldCloseMatch(board, cell, color);
                 reroll++)
            {
                color = ChipColors.FromIndex(random.NextInt(colorCount) + 1);
            }

            return MatchFreePlacement.FirstColorWithoutMatch(board, cell, colorCount, color);
        }

        /// <summary>
        /// Swaps two chips of different colours until a legal move appears without opening a
        /// match. Every pair is tried at most once, so the pass always terminates.
        /// </summary>
        private bool TryRepair()
        {
            for (int first = 0; first < _positions.Count; first++)
            {
                GridPos a = _positions[first];

                for (int second = first + 1; second < _positions.Count; second++)
                {
                    GridPos b = _positions[second];
                    if (_board.GetSlot(a).Color == _board.GetSlot(b).Color)
                    {
                        continue;
                    }

                    _board.SwapSlots(a, b);
                    _legalMoves.Invalidate();

                    if (IsPlayable())
                    {
                        return true;
                    }

                    _board.SwapSlots(a, b);
                }
            }

            _legalMoves.Invalidate();
            return false;
        }

        private bool IsPlayable() => !_detection.HasAnyMatch() && _legalMoves.HasAnyMove;

        /// <summary>One ChipMoved per relocated chip: identity travels, so the view can fly it.</summary>
        private void EmitMoves(TranscriptWriter writer)
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                int instanceId = _slots[i].InstanceId;
                GridPos target = _positions[i];
                GridPos origin = FindOrigin(instanceId);

                if (origin != target)
                {
                    writer.ChipMoved(origin, target, ChipMoveFlags.Shuffle, instanceId);
                }
            }
        }

        /// <summary>
        /// The regeneration phase changes colours in place, so identity does not travel: the view
        /// is told the old chip left and a new one arrived.
        /// </summary>
        private void EmitRecolours(TranscriptWriter writer)
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                GridPos cell = _positions[i];
                ChipSlot slot = _board.GetSlot(cell);

                // Origin, not _slots[i]: the failed permutation attempts were never emitted, so
                // the chip the view still believes is here is the one this cell started with.
                writer.ChipDestroyed(cell, _originColors[i], _originIds[i]);
                writer.ChipSpawned(cell, slot.Color, slot.InstanceId);
            }
        }

        private GridPos FindOrigin(int instanceId)
        {
            for (int i = 0; i < _originIds.Count; i++)
            {
                if (_originIds[i] == instanceId)
                {
                    return _originCells[i];
                }
            }

            return GridPos.Invalid;
        }
    }
}
