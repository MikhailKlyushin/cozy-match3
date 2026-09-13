using System;
using UnityEngine;

namespace Match3.Levels.Authoring
{
    /// <summary>
    /// Ordered level set. <see cref="LastLevelId"/> is computed from the data, so adding a 13th
    /// level changes nothing in code - a literal 12 anywhere is a review finding (§8.3).
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private LevelConfig[] _levels = Array.Empty<LevelConfig>();

        public int Count => _levels.Length;

        /// <summary>Largest id in the set. Winning this level opens End of content (§8.3).</summary>
        public int LastLevelId
        {
            get
            {
                int last = 0;
                for (int i = 0; i < _levels.Length; i++)
                {
                    if (_levels[i] != null && _levels[i].Id > last)
                    {
                        last = _levels[i].Id;
                    }
                }

                return last;
            }
        }

        public int FirstLevelId
        {
            get
            {
                int first = int.MaxValue;
                for (int i = 0; i < _levels.Length; i++)
                {
                    if (_levels[i] != null && _levels[i].Id < first)
                    {
                        first = _levels[i].Id;
                    }
                }

                return first == int.MaxValue ? 0 : first;
            }
        }

        public LevelConfig GetByIndex(int index) => _levels[index];

        public bool TryGetById(int id, out LevelConfig config)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i] != null && _levels[i].Id == id)
                {
                    config = _levels[i];
                    return true;
                }
            }

            config = null;
            return false;
        }

        public bool ContainsId(int id) => TryGetById(id, out _);

#if UNITY_EDITOR
        /// <summary>Editor-only: used by the level asset generator.</summary>
        internal void SetLevels(LevelConfig[] levels) => _levels = levels;
#endif
    }
}
