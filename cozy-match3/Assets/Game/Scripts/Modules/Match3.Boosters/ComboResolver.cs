using System;
using Match3.Board;
using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>
    /// Builds the plan of every cell of the §6.3 matrix. The epicentre is the swap target cell -
    /// the one the second booster lay in - and the matrix is symmetric, so (A,B) and (B,A) give
    /// the same plan. Targeting is delegated, never re-implemented (D09, A07).
    /// </summary>
    public sealed class ComboResolver
    {
        private readonly IBoardReader _board;
        private readonly ITargetingService _targeting;

        private readonly PooledList<ComboStep> _steps = new PooledList<ComboStep>(24);
        private readonly CellBuffer _direct = new CellBuffer();
        private readonly CellBuffer _targets = new CellBuffer();
        private readonly CellBuffer _colorCells = new CellBuffer();

        private const float NoDelay = 0f;

        /// <summary>§6.3: rockets rain at 0.06 s intervals.</summary>
        private const float RocketRainStepDelay = 0.06f;

        /// <summary>§6.3: bombs and airplanes rain at 0.08 s intervals.</summary>
        private const float HeavyRainStepDelay = 0.08f;

        /// <summary>Bomb plus bomb is one 7x7 blast.</summary>
        private const int BigBlastRadius = 3;

        /// <summary>Rocket plus bomb thickens the cross by one row and one column each way.</summary>
        private const int ThickCrossReach = 1;

        /// <summary>
        /// ResolveCaps.MaxAirplanesInCombo (§6.3). Restated here because Match3.Resolve references
        /// this assembly, so importing the constant would close an assembly cycle (I8).
        /// </summary>
        private const int MaxAirplanesInCombo = 8;

        private const int DeliveryTargets = 2;

        private const int PairAirplaneTargets = 3;

        public ComboResolver(IBoardReader board, ITargetingService targeting)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));

            _direct.Configure(board.Width, board.Height);
            _targets.Configure(board.Width, board.Height);
            _colorCells.Configure(board.Width, board.Height);
        }

        /// <summary>
        /// <paramref name="other"/> is the source cell of the swap: the plan geometry is centred
        /// on <paramref name="epicentre"/> alone, the pair is kept for the transcript.
        /// </summary>
        public ComboPlan Resolve(GridPos epicentre, BoosterType a, GridPos other, BoosterType b)
        {
            ComboKind kind = ComboMatrix.Kind(a, b);

            _steps.Clear();
            _direct.Clear();
            bool damagesEveryObstacle = false;

            switch (kind)
            {
                case ComboKind.Cross:
                    AddCross(epicentre);
                    break;
                case ComboKind.ThickCross:
                    AddThickCross(epicentre);
                    break;
                case ComboKind.BigBlast:
                    AddSquare(epicentre, BigBlastRadius);
                    break;
                case ComboKind.RocketRain:
                    AddRocketRain();
                    break;
                case ComboKind.BombRain:
                    AddRain(BoosterType.Bomb, HeavyRainStepDelay);
                    break;
                case ComboKind.WholeBoard:
                    AddEveryChip();
                    damagesEveryObstacle = true;
                    break;
                case ComboKind.RocketDelivery:
                    AddDelivery(BoosterType.RocketH, BoosterType.RocketV);
                    break;
                case ComboKind.BombDelivery:
                    AddDelivery(BoosterType.Bomb, BoosterType.Bomb);
                    break;
                case ComboKind.AirplaneRain:
                    AddAirplaneRain();
                    break;
                case ComboKind.ThreeAirplanes:
                    AddAirplanes(PairAirplaneTargets);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(a),
                        "Booster pair outside the combination matrix: " + a + " + " + b);
            }

            return new ComboPlan(epicentre, a, b, ToStepArray(), ToDirectArray(), damagesEveryObstacle);
        }

        /// <summary>Full row plus full column through the epicentre.</summary>
        private void AddCross(GridPos epicentre)
        {
            _steps.Add(new ComboStep(epicentre, BoosterType.RocketH, NoDelay, false));
            _steps.Add(new ComboStep(epicentre, BoosterType.RocketV, NoDelay, false));
        }

        /// <summary>Rows y-1..y+1 and columns x-1..x+1 in full, clipped by the board edge.</summary>
        private void AddThickCross(GridPos epicentre)
        {
            for (int y = epicentre.Y - ThickCrossReach; y <= epicentre.Y + ThickCrossReach; y++)
            {
                if (y >= 0 && y < _board.Height)
                {
                    _steps.Add(new ComboStep(new GridPos(epicentre.X, y), BoosterType.RocketH, NoDelay, false));
                }
            }

            for (int x = epicentre.X - ThickCrossReach; x <= epicentre.X + ThickCrossReach; x++)
            {
                if (x >= 0 && x < _board.Width)
                {
                    _steps.Add(new ComboStep(new GridPos(x, epicentre.Y), BoosterType.RocketV, NoDelay, false));
                }
            }
        }

        private void AddSquare(GridPos centre, int radius)
        {
            for (int y = centre.Y - radius; y <= centre.Y + radius; y++)
            {
                for (int x = centre.X - radius; x <= centre.X + radius; x++)
                {
                    BoosterCells.AddIfHittable(_board, _direct, new GridPos(x, y));
                }
            }
        }

        /// <summary>Orientation alternates along the y-up, x-up order: even index horizontal.</summary>
        private void AddRocketRain()
        {
            int count = CollectNeededColorCells();
            for (int i = 0; i < count; i++)
            {
                BoosterType booster = (i & 1) == 0 ? BoosterType.RocketH : BoosterType.RocketV;
                _steps.Add(new ComboStep(_colorCells[i], booster, RocketRainStepDelay, true));
            }
        }

        private void AddRain(BoosterType booster, float stepDelay)
        {
            int count = CollectNeededColorCells();
            for (int i = 0; i < count; i++)
            {
                _steps.Add(new ComboStep(_colorCells[i], booster, stepDelay, true));
            }
        }

        /// <summary>Cap 8 (§6.3): the first cells become airplanes, the rest are just destroyed.</summary>
        private void AddAirplaneRain()
        {
            int count = CollectNeededColorCells();
            for (int i = 0; i < count; i++)
            {
                GridPos cell = _colorCells[i];
                if (i < MaxAirplanesInCombo)
                {
                    _steps.Add(new ComboStep(cell, BoosterType.Airplane, HeavyRainStepDelay, true));
                    continue;
                }

                _direct.AddUnique(cell);
            }
        }

        /// <summary>Two airplanes to goal targets 1 and 2, each dropping its payload on impact.</summary>
        private void AddDelivery(BoosterType firstPayload, BoosterType secondPayload)
        {
            int count = PickTargets(DeliveryTargets);
            for (int i = 0; i < count; i++)
            {
                GridPos target = _targets[i];
                _steps.Add(new ComboStep(target, BoosterType.Airplane, NoDelay, false));
                _steps.Add(new ComboStep(target, i == 0 ? firstPayload : secondPayload, NoDelay, true));
            }
        }

        private void AddAirplanes(int count)
        {
            int picked = PickTargets(count);
            for (int i = 0; i < picked; i++)
            {
                _steps.Add(new ComboStep(_targets[i], BoosterType.Airplane, NoDelay, false));
            }
        }

        private void AddEveryChip()
        {
            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (_board.GetSlot(cell).IsChip)
                    {
                        _direct.AddUnique(cell);
                    }
                }
            }
        }

        /// <summary>Chips of the most needed colour (§6.1), in the order the delays follow.</summary>
        private int CollectNeededColorCells()
        {
            _colorCells.Clear();

            ChipColor color = _targeting.PickNeededColor();
            if (color == ChipColor.None)
            {
                return 0;
            }

            for (int y = 0; y < _board.Height; y++)
            {
                for (int x = 0; x < _board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    ChipSlot slot = _board.GetSlot(cell);
                    if (slot.IsChip && slot.Color == color)
                    {
                        _colorCells.AddUnique(cell);
                    }
                }
            }

            _colorCells.SortByYThenX();
            return _colorCells.Count;
        }

        private int PickTargets(int count)
        {
            _targets.Clear();
            return _targeting.PickGoalTargets(count, _targets);
        }

        private ComboStep[] ToStepArray()
        {
            if (_steps.Count == 0)
            {
                return Array.Empty<ComboStep>();
            }

            var steps = new ComboStep[_steps.Count];
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = _steps[i];
            }

            return steps;
        }

        private GridPos[] ToDirectArray()
        {
            if (_direct.Count == 0)
            {
                return Array.Empty<GridPos>();
            }

            var cells = new GridPos[_direct.Count];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = _direct[i];
            }

            return cells;
        }
    }
}
