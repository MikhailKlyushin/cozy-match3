using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.Gameplay
{
    /// <summary>
    /// Component pool. Chips, obstacles and FX come from here and nowhere else: on WebGL an
    /// Instantiate inside a cascade is a visible frame drop (§13, §14).
    /// </summary>
    public sealed class ViewPool<T> : IDisposable where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _free;
        private readonly List<T> _created;

        public ViewPool(T prefab, Transform parent, int prewarm)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            _free = new Stack<T>(prewarm);
            _created = new List<T>(prewarm);

            for (int i = 0; i < prewarm; i++)
            {
                Release(Create());
            }
        }

        /// <summary>Objects ever instantiated. A pool test asserts this stops growing.</summary>
        public int CreatedCount => _created.Count;

        public int FreeCount => _free.Count;

        public T Rent()
        {
            T instance = _free.Count > 0 ? _free.Pop() : Create();
            instance.gameObject.SetActive(true);
            return instance;
        }

        public void Release(T instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.gameObject.SetActive(false);
            if (_parent != null)
            {
                instance.transform.SetParent(_parent, false);
            }

            _free.Push(instance);
        }

        public void Dispose()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    UnityEngine.Object.Destroy(_created[i].gameObject);
                }
            }

            _created.Clear();
            _free.Clear();
        }

        private T Create()
        {
            T instance = UnityEngine.Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            _created.Add(instance);
            return instance;
        }
    }
}
