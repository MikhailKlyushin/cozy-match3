using System;
using System.Collections.Generic;
using Match3.Core;
using Match3.Goals;
using UnityEngine;

namespace Match3.Levels.Authoring
{
    /// <summary>
    /// Authoring asset for one level (GDD §10.1). The grid is stored as text so levels stay
    /// readable and diffable in git; a level editor is explicitly out of scope.
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Level Config", fileName = "Level")]
    public sealed class LevelConfig : ScriptableObject
    {
        [SerializeField] private int _id = 1;
        [SerializeField] private int _width = 7;
        [SerializeField] private int _height = 7;

        [Tooltip("4..6 (GDD §4.1). The strongest difficulty lever (§9.4).")]
        [SerializeField] private int _colorCount = 4;

        [Tooltip("Derived from the goal workload, not assigned by feel (GDD §9.3, D13).")]
        [SerializeField] private int _moveLimit = 20;

        [SerializeField] private DifficultyTier _tier = DifficultyTier.Easy;

        [Tooltip("Order is both the display order and the booster targeting priority (§8.2).")]
        [SerializeField] private GoalEntry[] _goals = Array.Empty<GoalEntry>();

        [Tooltip("Two-character tokens separated by spaces. FIRST ROW IS y = height - 1 (D01).")]
        [TextArea(9, 24)]
        [SerializeField] private string _layout = string.Empty;

        [Tooltip("Nested elements, outer to inner. Nesting is never written in the grid (§10.2).")]
        [SerializeField] private NestedContentEntry[] _contents = Array.Empty<NestedContentEntry>();

        [Tooltip("Spawner column indices. Empty means the §3.3 default.")]
        [SerializeField] private int[] _spawners = Array.Empty<int>();

        [Tooltip("Empty means uniform. Per-level data only, never a global constant (§4.1).")]
        [SerializeField] private ColorWeightEntry[] _spawnWeights = Array.Empty<ColorWeightEntry>();

        [Tooltip("Idle delay before the hint (§5.5). 0 disables it; levels 1-3 use 3 s.")]
        [SerializeField] private float _hintDelaySeconds = 5f;

        [Tooltip("Recorded reason whenever moveLimit deliberately differs from the §9.3 formula.")]
        [TextArea(2, 4)]
        [SerializeField] private string _balanceNotes = string.Empty;

        public int Id => _id;

        public int Width => _width;

        public int Height => _height;

        public int ColorCount => _colorCount;

        public int MoveLimit => _moveLimit;

        public DifficultyTier Tier => _tier;

        public IReadOnlyList<GoalEntry> Goals => _goals;

        public string Layout => _layout;

        public IReadOnlyList<NestedContentEntry> Contents => _contents;

        public IReadOnlyList<int> Spawners => _spawners;

        public IReadOnlyList<ColorWeightEntry> SpawnWeights => _spawnWeights;

        public float HintDelaySeconds => _hintDelaySeconds;

        public string BalanceNotes => _balanceNotes;

        /// <summary>Layout split into rows, blank lines dropped. Still top-down: the flip is the parser's job.</summary>
        public List<string> ReadLayoutRows()
        {
            var rows = new List<string>(_height);
            string[] lines = _layout.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length > 0)
                {
                    rows.Add(line);
                }
            }

            return rows;
        }

        [Serializable]
        public sealed class GoalEntry
        {
            [SerializeField] private GoalType _type = GoalType.CollectColor;
            [SerializeField] private int _target = 10;
            [SerializeField] private ChipColor _color = ChipColor.C1;
            [SerializeField] private string _token = string.Empty;
            [SerializeField] private BoosterType _booster = BoosterType.None;

            public GoalType Type => _type;

            public int Target => _target;

            public ChipColor Color => _color;

            public string Token => _token;

            public BoosterType Booster => _booster;

            public GoalDefinition ToDefinition()
            {
                switch (_type)
                {
                    case GoalType.CollectColor:
                        return GoalDefinition.CollectColor(_color, _target);
                    case GoalType.DestroyElement:
                        return GoalDefinition.DestroyElement(_token, _target);
                    case GoalType.DestroyAnyColoredBox:
                        return GoalDefinition.DestroyAnyColoredBox(_target);
                    case GoalType.ActivateBooster:
                        return GoalDefinition.ActivateBooster(_booster, _target);
                    default:
                        throw new InvalidOperationException("Unknown goal type: " + _type);
                }
            }
        }

        [Serializable]
        public sealed class NestedContentEntry
        {
            [SerializeField] private int _x;
            [SerializeField] private int _y;
            [SerializeField] private string _token = string.Empty;

            public int X => _x;

            public int Y => _y;

            public string Token => _token;
        }

        [Serializable]
        public sealed class ColorWeightEntry
        {
            [SerializeField] private ChipColor _color = ChipColor.C1;
            [SerializeField] private int _weight = 1;

            public ChipColor Color => _color;

            public int Weight => _weight;

            public ColorWeight ToColorWeight() => new ColorWeight(_color, _weight);
        }
    }
}
