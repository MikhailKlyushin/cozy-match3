using System;
using Match3.Progression;
using UnityEngine;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Creates and destroys the LevelContext object that owns one attempt (A08). Destroying it
    /// closes the sub-container, its R3 subscriptions and its tweens together, so "restart the
    /// level" is nothing more than a new context.
    /// </summary>
    public sealed class LevelContextFactory : ILevelContextFactory
    {
        private readonly DiContainer _container;
        private readonly LevelSessionRequestHolder _holder;
        private readonly GameObject _contextPrefab;
        private readonly Transform _parent;

        private GameObject _instance;

        public LevelContextFactory(
            DiContainer container,
            LevelSessionRequestHolder holder,
            GameObject contextPrefab,
            Transform parent)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _holder = holder ?? throw new ArgumentNullException(nameof(holder));
            _contextPrefab = contextPrefab != null
                ? contextPrefab
                : throw new ArgumentNullException(nameof(contextPrefab));
            _parent = parent;
        }

        public LevelSessionState Create(LevelSessionRequest request)
        {
            Destroy();

            // The installer picks the request up while the sub-container builds: a
            // GameObjectContext cannot take constructor arguments.
            _holder.Set(request);

            GameObjectContext context = _container.InstantiatePrefabForComponent<GameObjectContext>(
                _contextPrefab,
                _parent,
                Array.Empty<object>());

            _instance = context.gameObject;

            var session = context.Container.Resolve<LevelSessionState>();
            if (session == null)
            {
                throw new InvalidOperationException("LevelContext did not bind a LevelSessionState.");
            }

            return session;
        }

        public void Destroy()
        {
            if (_instance == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_instance);
            _instance = null;
        }
    }
}
