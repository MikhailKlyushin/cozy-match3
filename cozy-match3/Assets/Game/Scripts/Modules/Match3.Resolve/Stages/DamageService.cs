using System;
using System.Collections.Generic;
using Match3.Board;
using Match3.Core;
using BoardModel = Match3.Board.Board;

namespace Match3.Resolve
{
    /// <summary>
    /// Stage DAMAGE (GDD §5.1 stage 5). One damage instance per source per step (D07): a blast
    /// covering a box with three cells is 1 damage (E09), two different sources are 2 (E11), and
    /// the excess over the remaining hp is discarded rather than banked (§7.1).
    /// </summary>
    public sealed class DamageService
    {
        private readonly BoardModel _board;
        private readonly IDamageSourceRule[] _rulesByKind;
        private readonly IMatch3Logger _logger;
        private readonly PooledList<DamageMark> _marks = new PooledList<DamageMark>(32);
        private readonly GridPos[] _neighbours = new GridPos[4];

        /// <summary>D07: a source never deals more than this, whatever its area.</summary>
        private const int DamagePerSource = 1;

        private int _step;
        private bool _missingRuleReported;

        public DamageService(BoardModel board, IReadOnlyList<IDamageSourceRule> rules, IMatch3Logger logger)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _rulesByKind = BuildRegistry(rules);
        }

        /// <summary>Drops the source-to-element ledger: de-duplication is per step (D07).</summary>
        public void BeginStep(int step)
        {
            _step = step;
            _marks.Clear();
        }

        /// <summary>Damage from one match component; writes ElementDamaged for each hit element.</summary>
        public void ApplyMatchDamage(
            ChipColor matchColor,
            IReadOnlyList<GridPos> matchCells,
            int sourceId,
            TranscriptWriter writer)
            => Apply(
                matchCells,
                new DamageSource(DamageOriginKind.MatchComponent, sourceId),
                matchColor,
                isBoosterDamage: false,
                writer);

        /// <summary>Damage from one booster activation over the cells it covered (§6.2).</summary>
        public void ApplyBoosterDamage(IReadOnlyList<GridPos> hitCells, int sourceId, TranscriptWriter writer)
            => Apply(
                hitCells,
                new DamageSource(DamageOriginKind.BoosterActivation, sourceId),
                ChipColor.None,
                isBoosterDamage: true,
                writer);

        private static IDamageSourceRule[] BuildRegistry(IReadOnlyList<IDamageSourceRule> rules)
        {
            int maxKind = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null)
                {
                    throw new ArgumentException("Damage rule registry contains a null rule.", nameof(rules));
                }

                int kind = (int)rules[i].Kind;
                if (kind > maxKind)
                {
                    maxKind = kind;
                }
            }

            var byKind = new IDamageSourceRule[maxKind + 1];
            for (int i = 0; i < rules.Count; i++)
            {
                IDamageSourceRule rule = rules[i];
                int kind = (int)rule.Kind;
                if (byKind[kind] != null)
                {
                    throw new ArgumentException("Two damage rules claim the axis value " + rule.Kind, nameof(rules));
                }

                byKind[kind] = rule;
            }

            return byKind;
        }

        private void Apply(
            IReadOnlyList<GridPos> cells,
            in DamageSource source,
            ChipColor sourceColor,
            bool isBoosterDamage,
            TranscriptWriter writer)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            for (int i = 0; i < cells.Count; i++)
            {
                GridPos cell = cells[i];
                if (!_board.Contains(cell))
                {
                    continue;
                }

                TryDamage(cell, source, sourceColor, isBoosterDamage, isOnElementCell: true, writer);

                // Orthogonal neighbours only, already in y-then-x order; diagonals are not
                // adjacent (§7.1).
                int count = _board.GetOrthogonalNeighbours(cell, _neighbours);
                for (int n = 0; n < count; n++)
                {
                    TryDamage(_neighbours[n], source, sourceColor, isBoosterDamage, isOnElementCell: false, writer);
                }
            }
        }

        private void TryDamage(
            GridPos cell,
            in DamageSource source,
            ChipColor sourceColor,
            bool isBoosterDamage,
            bool isOnElementCell,
            TranscriptWriter writer)
        {
            if (!_board.TryGetElement(cell, out ElementInstance element) || !element.IsAlive)
            {
                return;
            }

            // Health axis, not a token check: infinite hp takes no damage and does not stop the
            // source either (D11, E08).
            if (element.IsIndestructible)
            {
                return;
            }

            // A nested element revealed in this step is immune until the step ends (§7.1).
            if (element.IsImmuneAtStep(_step))
            {
                return;
            }

            ElementDefinition definition = _board.Catalog.Get(element.Definition);
            IDamageSourceRule rule = ResolveRule(definition.DamageSource);
            if (rule == null)
            {
                ReportMissingRule();
                return;
            }

            var ctx = new DamageContext(
                _board,
                cell,
                element,
                definition,
                source,
                sourceColor,
                isBoosterDamage,
                isOnElementCell);

            if (!rule.IsDamagedBy(ctx))
            {
                return;
            }

            if (!TryMark(source, _board.CellAt(cell).ElementIndex))
            {
                return;
            }

            element.Health -= DamagePerSource;
            writer.ElementDamaged(cell, element.Definition, DamagePerSource, element.Health);
        }

        private IDamageSourceRule ResolveRule(DamageSourceKind kind)
        {
            int index = (int)kind;
            return index < _rulesByKind.Length ? _rulesByKind[index] : null;
        }

        /// <summary>Returns false when this source already damaged this element in this step (D07).</summary>
        private bool TryMark(in DamageSource source, int elementIndex)
        {
            for (int i = 0; i < _marks.Count; i++)
            {
                ref DamageMark mark = ref _marks[i];
                if (mark.ElementIndex == elementIndex && mark.SourceId == source.Id && mark.Kind == source.Kind)
                {
                    return false;
                }
            }

            _marks.Add(new DamageMark(source.Kind, source.Id, elementIndex));
            return true;
        }

        private void ReportMissingRule()
        {
            if (_missingRuleReported)
            {
                return;
            }

            _missingRuleReported = true;
            _logger.Error("DamageService: a damageSource axis value has no rule; the element takes no damage.");
        }

        /// <summary>
        /// One (source, element) pair already charged in this step. Kept in an append-ordered
        /// array rather than a HashSet, whose traversal order is banned in gameplay (I4).
        /// </summary>
        private readonly struct DamageMark
        {
            public readonly DamageOriginKind Kind;
            public readonly int SourceId;
            public readonly int ElementIndex;

            public DamageMark(DamageOriginKind kind, int sourceId, int elementIndex)
            {
                Kind = kind;
                SourceId = sourceId;
                ElementIndex = elementIndex;
            }
        }
    }
}
