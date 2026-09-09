using System;
using Match3.Board;
using Match3.Core;
using Match3.Matching;
using BoardModel = Match3.Board.Board;

namespace Match3.Levels
{
    /// <summary>
    /// LevelData + IRandom -> Board (E20). Random cells are filled y-up then x-up; a colour that
    /// would close a match is re-rolled; the board is regenerated until a legal move exists and,
    /// as a last resort, repaired by swapping two chips. RNG order is §13: board generation
    /// first, then the cx starting colours.
    /// </summary>
    public sealed class BoardBuilder
    {
        private readonly ElementCatalog _catalog;
        private readonly IMatch3Logger _logger;
        private readonly PooledList<GridPos> _randomCells = new PooledList<GridPos>(81);
        private readonly PooledList<GridPos> _elementCells = new PooledList<GridPos>(32);
        private readonly PooledList<GridPos> _chipCells = new PooledList<GridPos>(81);

        /// <summary>
        /// Mirrors ResolveCaps.MaxBoardGenerationAttempts (§13, E20). Duplicated as a constant
        /// because Match3.Levels must not reference Match3.Resolve (A06).
        /// </summary>
        private const int MaxGenerationAttempts = 50;

        private const int MinLineLength = 3;

        public BoardBuilder(ElementCatalog catalog, IMatch3Logger logger)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public BoardModel Build(LevelData level, IRandom random)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (level.ColorCount < ChipColors.MinColorCount || level.ColorCount > ChipColors.MaxColorCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(level),
                    "colorCount must be in [" + ChipColors.MinColorCount + ", " + ChipColors.MaxColorCount
                    + "], got " + level.ColorCount + ".");
            }

            TokenGrid grid = LayoutParser.Parse(level.LayoutRows, level.Width, level.Height, _catalog);
            var board = new BoardModel(level.Width, level.Height, _catalog);

            ApplyTokens(board, grid);
            ApplyContents(board, level);
            ApplySpawners(board, level);

            var detection = new MatchDetectionService(board);
            var moves = new LegalMoveService(board, new SwapValidator(board, detection));

            GenerateChips(board, level.ColorCount, random, detection, moves);
            ApplyCyclingColors(board, level.ColorCount, random);

            return board;
        }

        private void ApplyTokens(BoardModel board, TokenGrid grid)
        {
            _randomCells.Clear();
            _elementCells.Clear();

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    string token = grid.TokenAt(x, y);

                    if (LevelTokens.IsHole(token))
                    {
                        board.SetKind(cell, CellKind.Hole);
                        continue;
                    }

                    board.SetKind(cell, CellKind.Playable);

                    if (LevelTokens.IsPlayable(token))
                    {
                        _randomCells.Add(cell);
                        continue;
                    }

                    if (LevelTokens.TryParseFixedChip(token, out ChipColor color))
                    {
                        // An explicit colour is authored, never re-rolled (§10.2).
                        board.SetChip(cell, color);
                        continue;
                    }

                    if (LevelTokens.TryParseBooster(token, out BoosterType booster))
                    {
                        board.SetBooster(cell, booster);
                        continue;
                    }

                    if (_catalog.TryResolve(token, out ElementDefinition definition))
                    {
                        int index = board.AddElement(
                            definition.Id,
                            LevelTokens.InitialElementColor(definition),
                            ElementInstance.NoNested);
                        board.AttachElement(cell, index);
                        _elementCells.Add(cell);
                        continue;
                    }

                    throw new ArgumentException("Unhandled layout token '" + token + "' at " + cell + ".");
                }
            }
        }

        /// <summary>GDD §10.2: nesting comes from the contents list, outer to inner, never from the grid.</summary>
        private void ApplyContents(BoardModel board, LevelData level)
        {
            for (int i = 0; i < level.Contents.Count; i++)
            {
                NestedContent content = level.Contents[i];
                var cell = new GridPos(content.X, content.Y);

                if (!board.Contains(cell))
                {
                    throw new ArgumentException("Nested content " + i + " is outside the board: " + cell + ".");
                }

                if (!_catalog.TryResolve(content.Token, out ElementDefinition definition))
                {
                    throw new ArgumentException(
                        "Nested content " + i + " at " + cell + " has unknown token '" + content.Token + "'.");
                }

                if (!board.TryGetElement(cell, out ElementInstance outer))
                {
                    throw new ArgumentException(
                        "Nested content " + i + " at " + cell + " has no outer element in the layout.");
                }

                ElementInstance deepest = outer;
                while (deepest.NestedIndex != ElementInstance.NoNested)
                {
                    deepest = board.ElementAt(deepest.NestedIndex);
                }

                deepest.NestedIndex = board.AddElement(
                    definition.Id,
                    LevelTokens.InitialElementColor(definition),
                    ElementInstance.NoNested);
            }
        }

        private static void ApplySpawners(BoardModel board, LevelData level)
        {
            if (level.Spawners.Count > 0)
            {
                board.SetSpawnerColumns(level.Spawners);
                return;
            }

            board.RecomputeDefaultSpawners();
        }

        private void GenerateChips(
            BoardModel board,
            int colorCount,
            IRandom random,
            MatchDetectionService detection,
            LegalMoveService moves)
        {
            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                FillRandomCells(board, colorCount, random);
                moves.Invalidate();
                if (moves.HasAnyMove)
                {
                    return;
                }

                if (_randomCells.Count == 0)
                {
                    // Nothing to re-roll: every further attempt would rebuild the same board.
                    break;
                }
            }

            if (!TryRepair(board, detection, moves))
            {
                _logger.Error("Board generation found no legal move and the repair pass failed.");
            }
        }

        private void FillRandomCells(BoardModel board, int colorCount, IRandom random)
        {
            for (int i = 0; i < _randomCells.Count; i++)
            {
                board.ClearSlot(_randomCells[i]);
            }

            for (int i = 0; i < _randomCells.Count; i++)
            {
                GridPos cell = _randomCells[i];
                board.SetChip(cell, PickColor(board, cell, colorCount, random));
            }
        }

        /// <summary>
        /// E20: re-roll a colour that would close a match, at most colorCount - 1 times, then
        /// take any colour that does not close one. A closed match here is a line of 3 or a 2x2
        /// square — both are primitives (§4.2) and §3.4 rule 2 forbids either in the initial board.
        /// </summary>
        private static ChipColor PickColor(BoardModel board, GridPos cell, int colorCount, IRandom random)
        {
            ChipColor color = ChipColors.FromIndex(random.NextInt(colorCount) + 1);
            for (int reroll = 0; reroll < colorCount - 1 && WouldCloseMatch(board, cell, color); reroll++)
            {
                color = ChipColors.FromIndex(random.NextInt(colorCount) + 1);
            }

            if (!WouldCloseMatch(board, cell, color))
            {
                return color;
            }

            for (int index = 1; index <= colorCount; index++)
            {
                ChipColor candidate = ChipColors.FromIndex(index);
                if (!WouldCloseMatch(board, cell, candidate))
                {
                    return candidate;
                }
            }

            // Fully constrained cell: the validator reports the ready-made match (§3.4 rule 2).
            return color;
        }

        /// <summary>
        /// Repair pass (§13, E20): swap two chips until a legal move exists. Every pair is tried
        /// at most once, so the pass always terminates.
        /// </summary>
        private bool TryRepair(BoardModel board, MatchDetectionService detection, LegalMoveService moves)
        {
            moves.Invalidate();
            if (moves.HasAnyMove)
            {
                return true;
            }

            CollectChipCells(board);

            for (int first = 0; first < _chipCells.Count; first++)
            {
                GridPos a = _chipCells[first];
                for (int second = first + 1; second < _chipCells.Count; second++)
                {
                    GridPos b = _chipCells[second];
                    if (board.GetSlot(a).Color == board.GetSlot(b).Color)
                    {
                        continue;
                    }

                    board.SwapSlots(a, b);
                    moves.Invalidate();
                    if (!detection.HasAnyMatch() && moves.HasAnyMove)
                    {
                        return true;
                    }

                    board.SwapSlots(a, b);
                }
            }

            moves.Invalidate();
            return false;
        }

        private void CollectChipCells(BoardModel board)
        {
            _chipCells.Clear();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (board.GetSlot(cell).Kind == SlotKind.Chip && board.IsMovable(cell))
                    {
                        _chipCells.Add(cell);
                    }
                }
            }
        }

        /// <summary>GDD §7.2: cx boxes are desynchronised, one RNG draw each, after generation (§13).</summary>
        private void ApplyCyclingColors(BoardModel board, int colorCount, IRandom random)
        {
            for (int i = 0; i < _elementCells.Count; i++)
            {
                if (!board.TryGetElement(_elementCells[i], out ElementInstance element))
                {
                    continue;
                }

                while (element != null)
                {
                    if (_catalog.Get(element.Definition).ColorMode == ElementColorMode.Cycling)
                    {
                        element.CurrentColor = ChipColors.FromIndex(random.NextInt(colorCount) + 1);
                    }

                    element = element.NestedIndex == ElementInstance.NoNested
                        ? null
                        : board.ElementAt(element.NestedIndex);
                }
            }
        }

        private static bool WouldCloseMatch(BoardModel board, GridPos cell, ChipColor color)
        {
            if (RunLength(board, cell, color, -1, 0) + 1 + RunLength(board, cell, color, 1, 0) >= MinLineLength)
            {
                return true;
            }

            if (RunLength(board, cell, color, 0, -1) + 1 + RunLength(board, cell, color, 0, 1) >= MinLineLength)
            {
                return true;
            }

            return ClosesSquare(board, cell, color);
        }

        private static int RunLength(BoardModel board, GridPos cell, ChipColor color, int stepX, int stepY)
        {
            int length = 0;
            var next = new GridPos(cell.X + stepX, cell.Y + stepY);
            while (ColorAt(board, next) == color)
            {
                length++;
                next = new GridPos(next.X + stepX, next.Y + stepY);
            }

            return length;
        }

        private static bool ClosesSquare(BoardModel board, GridPos cell, ChipColor color)
        {
            for (int offsetY = -1; offsetY <= 0; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 0; offsetX++)
                {
                    var origin = new GridPos(cell.X + offsetX, cell.Y + offsetY);
                    if (!board.Contains(origin) || !board.Contains(new GridPos(origin.X + 1, origin.Y + 1)))
                    {
                        continue;
                    }

                    if (IsSquareOfColor(board, origin, cell, color))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsSquareOfColor(BoardModel board, GridPos origin, GridPos placed, ChipColor color)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int x = 0; x <= 1; x++)
                {
                    var corner = new GridPos(origin.X + x, origin.Y + y);
                    if (corner != placed && ColorAt(board, corner) != color)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static ChipColor ColorAt(BoardModel board, GridPos cell)
        {
            if (!board.Contains(cell) || board.GetKind(cell) != CellKind.Playable)
            {
                return ChipColor.None;
            }

            ChipSlot slot = board.GetSlot(cell);
            return slot.Kind == SlotKind.Chip ? slot.Color : ChipColor.None;
        }
    }
}
