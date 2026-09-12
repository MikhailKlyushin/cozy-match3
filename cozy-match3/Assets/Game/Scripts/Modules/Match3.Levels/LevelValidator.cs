using System;
using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Matching;
using BoardModel = Match3.Board.Board;

namespace Match3.Levels
{
    /// <summary>
    /// GDD §3.4 load-time validation: every playable cell is fed by a spawner, the initial board
    /// holds no ready-made match and at least one legal move, goals are physically reachable,
    /// nesting depth is at most 3, colorCount is in [4, 6], sizes match and tokens are known.
    /// The §10.3 wall-width rule is reported as a warning. A broken level must fail loudly on
    /// load rather than produce a slightly broken board.
    /// </summary>
    public sealed class LevelValidator
    {
        private readonly ElementCatalog _catalog;
        private readonly FlowReachabilityAnalyzer _flow;
        private readonly BoardBuilder _builder;
        private readonly PooledList<ValidationIssue> _issues = new PooledList<ValidationIssue>(8);

        /// <summary>GDD §3.4 rule 4.</summary>
        private const int MaxNestingDepth = 3;

        /// <summary>GDD §10.3: cells under a solid wall block wider than this are never refilled.</summary>
        private const int MaxWallWidth = 2;

        public LevelValidator(ElementCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _flow = new FlowReachabilityAnalyzer(catalog);
            _builder = new BoardBuilder(catalog, NullLogger.Instance);
        }

        /// <summary>Consumes <paramref name="random"/>: the initial-board checks build the board.</summary>
        public ValidationReport Validate(LevelData level, IRandom random)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            _issues.Clear();

            ValidateColorCount(level);
            TokenGrid grid = TryParse(level);

            if (grid != null)
            {
                ValidateSpawners(level);
                ValidateNesting(level, grid);
                ValidateReachability(level, grid);
                ValidateWallWidth(grid);
                ValidateGoals(level, grid);
            }

            // The board is only built once the structure is sound, otherwise Build would throw.
            if (!HasErrors())
            {
                ValidateInitialBoard(level, random);
            }

            return new ValidationReport(_issues);
        }

        private void ValidateColorCount(LevelData level)
        {
            if (level.ColorCount < ChipColors.MinColorCount || level.ColorCount > ChipColors.MaxColorCount)
            {
                Error("colorCount must be in [" + ChipColors.MinColorCount + ", " + ChipColors.MaxColorCount
                      + "] (§4.1), got " + level.ColorCount + ".");
            }
        }

        /// <summary>Size mismatches, token lengths and unknown tokens are all parser errors (§10.2).</summary>
        private TokenGrid TryParse(LevelData level)
        {
            try
            {
                return LayoutParser.Parse(level.LayoutRows, level.Width, level.Height, _catalog);
            }
            catch (ArgumentException e)
            {
                Error(e.Message);
                return null;
            }
        }

        private void ValidateSpawners(LevelData level)
        {
            for (int i = 0; i < level.Spawners.Count; i++)
            {
                int column = level.Spawners[i];
                if (column < 0 || column >= level.Width)
                {
                    Error("spawner column " + column + " is outside the board width " + level.Width + " (§3.3).");
                }
            }
        }

        private void ValidateNesting(LevelData level, TokenGrid grid)
        {
            for (int i = 0; i < level.Contents.Count; i++)
            {
                NestedContent content = level.Contents[i];
                var cell = new GridPos(content.X, content.Y);

                if (content.X < 0 || content.X >= grid.Width || content.Y < 0 || content.Y >= grid.Height)
                {
                    Error("contents[" + i + "] is outside the board: " + cell + ".");
                    continue;
                }

                if (!_catalog.TryResolve(content.Token, out ElementDefinition _))
                {
                    Error("contents[" + i + "] at " + cell + " has unknown element token '" + content.Token + "'.");
                }

                if (!_catalog.TryResolve(grid.TokenAt(content.X, content.Y), out ElementDefinition _))
                {
                    Error("contents[" + i + "] at " + cell + " has no outer element in the layout (§10.2).");
                }
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!_catalog.TryResolve(grid.TokenAt(x, y), out ElementDefinition _))
                    {
                        continue;
                    }

                    int depth = 1 + CountContentsAt(level, x, y);
                    if (depth > MaxNestingDepth)
                    {
                        Error("nesting depth " + depth + " at " + new GridPos(x, y) + " exceeds "
                              + MaxNestingDepth + " (§3.4 rule 4).");
                    }
                }
            }
        }

        private void ValidateReachability(LevelData level, TokenGrid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    string token = grid.TokenAt(x, y);

                    // Holes are not cells and the blocker occupies its cell forever (§3.2).
                    if (LevelTokens.IsPermanentObstruction(token, _catalog))
                    {
                        continue;
                    }

                    if (!_flow.IsReachableFromSpawner(grid, level.Spawners, x, y))
                    {
                        Error("cell " + new GridPos(x, y)
                              + " is a dead pocket: no spawner feeds it through the §5.3 flow (§3.4 rule 1).");
                    }
                }
            }
        }

        private void ValidateWallWidth(TokenGrid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                int x = 0;
                while (x < grid.Width)
                {
                    // A blocker run is harmless: its own column still feeds the cells below it
                    // (§5.3), so only a run that cuts the column is worth a warning.
                    if (!LevelTokens.BlocksColumnFlow(grid.TokenAt(x, y), _catalog))
                    {
                        x++;
                        continue;
                    }

                    int start = x;
                    while (x < grid.Width && LevelTokens.BlocksColumnFlow(grid.TokenAt(x, y), _catalog))
                    {
                        x++;
                    }

                    int runWidth = x - start;
                    if (runWidth > MaxWallWidth && HasCellBelow(grid, start, runWidth, y))
                    {
                        Warning("solid wall block of width " + runWidth + " at y = " + y + ", x = " + start + ".."
                                + (x - 1) + ": cells under a block wider than " + MaxWallWidth
                                + " are refilled only from its outer sides (§10.3).");
                    }
                }
            }
        }

        private void ValidateGoals(LevelData level, TokenGrid grid)
        {
            for (int i = 0; i < level.Goals.Count; i++)
            {
                GoalDefinition goal = level.Goals[i];
                switch (goal.Type)
                {
                    case GoalType.CollectColor:
                        if (goal.Color == ChipColor.None || ChipColors.ToIndex(goal.Color) > level.ColorCount)
                        {
                            Error("goals[" + i + "] collects colour " + goal.Color + ", which is outside colorCount "
                                  + level.ColorCount + " (§8.1).");
                        }

                        break;
                    case GoalType.DestroyElement:
                        ValidateElementGoal(level, grid, i, goal);
                        break;
                    case GoalType.DestroyAnyColoredBox:
                        ValidateSupply(level, grid, i, goal);
                        break;
                    default:
                        // ActivateBooster counts activations: boosters come from matches, not from the layout (Q1).
                        break;
                }
            }
        }

        private void ValidateElementGoal(LevelData level, TokenGrid grid, int index, in GoalDefinition goal)
        {
            if (!_catalog.TryResolve(goal.Token, out ElementDefinition definition))
            {
                Error("goals[" + index + "] targets unknown element token '" + goal.Token + "'.");
                return;
            }

            if (definition.GoalRole == GoalRole.NotCountable)
            {
                Error("goals[" + index + "] targets '" + goal.Token + "', which can never be a goal (§7.2).");
                return;
            }

            ValidateSupply(level, grid, index, goal);
        }

        /// <summary>GDD §3.4 rule 3: the board must hold at least the target number of units.</summary>
        private void ValidateSupply(LevelData level, TokenGrid grid, int index, in GoalDefinition goal)
        {
            int available = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (LevelTokens.ServesGoal(grid.TokenAt(x, y), goal, _catalog))
                    {
                        available++;
                    }
                }
            }

            for (int i = 0; i < level.Contents.Count; i++)
            {
                if (LevelTokens.ServesGoal(level.Contents[i].Token, goal, _catalog))
                {
                    available++;
                }
            }

            if (available < goal.Target)
            {
                Error("goals[" + index + "] needs " + goal.Target + " units but the level holds " + available
                      + " (§3.4 rule 3).");
            }
        }

        private void ValidateInitialBoard(LevelData level, IRandom random)
        {
            BoardModel board = _builder.Build(level, random);
            var detection = new MatchDetectionService(board);

            if (detection.HasAnyMatch())
            {
                Error("initial board contains a ready-made match (§3.4 rule 2).");
            }

            var moves = new LegalMoveService(board, new SwapValidator(board, detection));
            if (!moves.HasAnyMove)
            {
                Error("initial board has no legal move (§3.4 rule 2, E20).");
            }
        }

        private static int CountContentsAt(LevelData level, int x, int y)
        {
            int count = 0;
            for (int i = 0; i < level.Contents.Count; i++)
            {
                NestedContent content = level.Contents[i];
                if (content.X == x && content.Y == y)
                {
                    count++;
                }
            }

            return count;
        }

        private bool HasCellBelow(TokenGrid grid, int start, int runWidth, int y)
        {
            if (y == 0)
            {
                return false;
            }

            for (int x = start; x < start + runWidth; x++)
            {
                if (!LevelTokens.IsPermanentObstruction(grid.TokenAt(x, y - 1), _catalog))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasErrors()
        {
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].Severity == ValidationSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private void Error(string message) => _issues.Add(new ValidationIssue(ValidationSeverity.Error, message));

        private void Warning(string message) => _issues.Add(new ValidationIssue(ValidationSeverity.Warning, message));
    }
}
