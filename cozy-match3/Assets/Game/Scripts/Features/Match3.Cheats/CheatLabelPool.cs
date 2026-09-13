using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Match3.Cheats
{
    /// <summary>
    /// Pool for the coordinate-overlay labels: the overlay is toggled repeatedly while looking at
    /// a level, and instantiating a label per cell per toggle is the allocation §14 forbids.
    /// Gameplay's <c>ViewPool</c> is a class and Match3.Cheats may use Gameplay's interfaces only
    /// (§3.1), so the panel carries its own three-method copy.
    /// </summary>
    internal sealed class CheatLabelPool : IDisposable
    {
        private readonly TMP_Text _prefab;
        private readonly RectTransform _parent;
        private readonly Stack<TMP_Text> _free;
        private readonly List<TMP_Text> _created;

        public CheatLabelPool(TMP_Text prefab, RectTransform parent, int prewarm)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            _free = new Stack<TMP_Text>(prewarm);
            _created = new List<TMP_Text>(prewarm);

            for (int i = 0; i < prewarm; i++)
            {
                Release(Create());
            }
        }

        /// <summary>Objects ever instantiated; stops growing once the largest board was shown.</summary>
        public int CreatedCount => _created.Count;

        public int FreeCount => _free.Count;

        public TMP_Text Rent()
        {
            TMP_Text label = _free.Count > 0 ? _free.Pop() : Create();
            label.gameObject.SetActive(true);
            return label;
        }

        public void Release(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            label.gameObject.SetActive(false);
            _free.Push(label);
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

        /// <summary>
        /// Anchors are forced to the centre here: the overlay positions labels by
        /// <c>anchoredPosition</c> converted from a screen point, which only lines up when the
        /// label's anchor and pivot sit in the middle.
        /// </summary>
        private TMP_Text Create()
        {
            TMP_Text label = UnityEngine.Object.Instantiate(_prefab, _parent);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            label.raycastTarget = false;
            label.gameObject.SetActive(false);
            _created.Add(label);
            return label;
        }
    }
}
