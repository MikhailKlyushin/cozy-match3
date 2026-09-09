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
        private readonly IRandom _random;
        private readonly IMatch3Logger _logger;

        private readonly List<GridPos> _positions = new List<GridPos>(96);
        private readonly List<ChipSlot> _slots = new List<ChipSlot>(96);
        private readonly List<GridPos> _originCells = new List<GridPos>(96);
        private readonly List<int> _originIds = new List<int>(96);

        public ShuffleService(
            BoardModel board,
            MatchDetectionService detection,
            LegalMoveService legalMoves,
            IChipSpawnPolicy spawnPolicy,
            IRandom random,
            IMatch3Logger logger)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _detection = detection ?? throw new ArgumentNullException(nameof(detection));
            _legalMoves = legalMoves ?? throw new ArgumentNullException(nameof(legalMoves));
            _spawnPolicy = spawnPolicy ?? throw new ArgumentNullException(nameof(spawnPolicy));
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

            writer.ShuffleEnd();
            _logger.Error("Shuffle exhausted both phases without restoring a legal move (E19)");
            return false;
        }

        private void Collect()
        {
            _positions.Clear();
            _slots.Clear();
            _originCells.Clear();
            _originIds.Clear();

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
                _board.SetChip(cell, color, _originIds[i]);
            }

            _legalMoves.Invalidate();
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
                writer.ChipDestroyed(cell, _slots[i].Color, _originIds[i]);
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
