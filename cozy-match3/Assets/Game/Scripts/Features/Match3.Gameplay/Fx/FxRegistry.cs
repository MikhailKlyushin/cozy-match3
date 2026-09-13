using System;
using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Gameplay
{
    /// <summary>
    /// One <see cref="ViewPool{T}"/> per FX prefab. FX are rented, never instantiated on the
    /// spot: an Instantiate inside a cascade is a visible frame drop on WebGL (§14).
    /// </summary>
    public sealed class FxRegistry : IDisposable
    {
        private readonly BoardView _board;
        private readonly IMatch3Logger _logger;

        private readonly Dictionary<FxView, ViewPool<FxView>> _poolByPrefab =
            new Dictionary<FxView, ViewPool<FxView>>(8);

        private readonly Dictionary<FxView, ViewPool<FxView>> _poolByInstance =
            new Dictionary<FxView, ViewPool<FxView>>(64);

        private readonly List<ViewPool<FxView>> _pools = new List<ViewPool<FxView>>(8);

        private bool _missingPrefabLogged;

        private const int DefaultPrewarm = 8;

        public FxRegistry(BoardView board, IMatch3Logger logger)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_board.FxLayer == null)
            {
                // FX are cosmetic, so this degrades instead of failing the level - but silently
                // mis-parenting pooled FX into the scene root would be far harder to find.
                _logger.Error("FxRegistry: BoardView has no FX layer, so booster FX are disabled.");
            }
        }

        /// <summary>Pools created so far; a pool test asserts this and the pools stop growing.</summary>
        public int PoolCount => _pools.Count;

        public int RentedCount => _poolByInstance.Count;

        /// <summary>Creates the prefab's pool ahead of the first cascade. Call at level build.</summary>
        public void Prewarm(GameObject prefab, int count) => PoolFor(prefab, count);

        /// <summary>Null when the prefab is missing: the timing still plays, only without FX.</summary>
        public FxView Rent(GameObject prefab)
        {
            ViewPool<FxView> pool = PoolFor(prefab, DefaultPrewarm);
            if (pool == null)
            {
                return null;
            }

            FxView view = pool.Rent();
            _poolByInstance[view] = pool;
            return view;
        }

        /// <summary>Idempotent, so a cancelled cascade can release in a finally without checks.</summary>
        public void Release(FxView view)
        {
            if (view == null || !_poolByInstance.TryGetValue(view, out ViewPool<FxView> pool))
            {
                return;
            }

            _poolByInstance.Remove(view);
            view.Stop();
            pool.Release(view);
        }

        public void Dispose()
        {
            for (int i = 0; i < _pools.Count; i++)
            {
                _pools[i].Dispose();
            }

            _pools.Clear();
            _poolByPrefab.Clear();
            _poolByInstance.Clear();
        }

        private ViewPool<FxView> PoolFor(GameObject prefab, int prewarm)
        {
            RectTransform parent = _board.FxLayer;
            FxView prefabView = prefab != null ? prefab.GetComponent<FxView>() : null;

            if (parent == null || prefabView == null)
            {
                WarnMissingPrefab();
                return null;
            }

            if (_poolByPrefab.TryGetValue(prefabView, out ViewPool<FxView> pool))
            {
                return pool;
            }

            pool = new ViewPool<FxView>(prefabView, parent, prewarm);
            _poolByPrefab.Add(prefabView, pool);
            _pools.Add(pool);
            return pool;
        }

        private void WarnMissingPrefab()
        {
            if (_missingPrefabLogged)
            {
                return;
            }

            _missingPrefabLogged = true;
            _logger.Warn("FxRegistry: an FX prefab is missing or carries no FxView; booster timing plays without FX.");
        }
    }
}
