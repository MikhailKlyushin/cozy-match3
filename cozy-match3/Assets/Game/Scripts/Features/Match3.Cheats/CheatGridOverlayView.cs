using System.Collections.Generic;
using System.Text;
using Match3.Core;
using Match3.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Cheats
{
    /// <summary>
    /// The "показать сетку координат" overlay of §12: an <c>(x, y)</c> label over every cell, for
    /// checking a level against its config (D01). Positions come from
    /// <see cref="IBoardCellPicker.CellToScreen"/>, the one board contract Gameplay exposes -
    /// there is no board-overlay interface and the cheat panel must not add one to Gameplay (§3.1).
    /// </summary>
    public sealed class CheatGridOverlayView : MonoBehaviour
    {
        [SerializeField] private RectTransform _labelRoot;
        [SerializeField] private Text _labelPrefab;
        [SerializeField] private int _labelPrewarm = 64;

        private readonly List<Text> _active = new List<Text>(64);
        private readonly StringBuilder _text = new StringBuilder(8);

        private CheatLabelPool _pool;
        private IBoardCellPicker _picker;
        private Camera _camera;
        private Vector2 _probeMin;
        private Vector2 _probeMax;
        private int _width;
        private int _height;
        private bool _visible;

        /// <summary>Screen-space movement below this is rounding noise, not a layout change.</summary>
        private const float ProbeEpsilon = 0.5f;

        /// <summary>True once the overlay was asked for; the labels appear as soon as the board has a layout.</summary>
        public bool IsVisible => _visible;

        /// <summary>Labels ever instantiated; stops growing once the largest board was shown.</summary>
        public int CreatedLabelCount => _pool != null ? _pool.CreatedCount : 0;

        /// <summary>Draws the overlay for the attempt's board; a null picker hides it.</summary>
        public void Show(IBoardCellPicker picker)
        {
            _picker = picker;

            if (picker == null)
            {
                Hide();
                return;
            }

            _visible = true;
            TryRebuild();
        }

        public void Hide()
        {
            _visible = false;
            _picker = null;
            ReleaseLabels();
        }

        private void Awake()
        {
            if (_labelRoot == null)
            {
                _labelRoot = transform as RectTransform;
            }

            if (_labelPrefab != null)
            {
                // The template lives inside the prefab; it must never render on its own.
                _labelPrefab.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The board moves on a resolution or orientation change and resizes on a new level, so
        /// the overlay re-measures instead of caching the layout once.
        /// </summary>
        private void LateUpdate()
        {
            if (!_visible || _picker == null)
            {
                return;
            }

            if (_active.Count == 0 || _picker.BoardWidth != _width || _picker.BoardHeight != _height)
            {
                TryRebuild();
                return;
            }

            if (HasBoardMoved())
            {
                Reposition();
            }
        }

        private void OnDestroy()
        {
            _active.Clear();

            if (_pool != null)
            {
                _pool.Dispose();
                _pool = null;
            }
        }

        /// <summary>
        /// The board gets its layout a frame or two after the attempt is built, so a rebuild that
        /// finds no board is retried rather than dropped - otherwise the toggle silently does nothing.
        /// </summary>
        private void TryRebuild()
        {
            if (!EnsurePool())
            {
                // Broken prefab: stop asking every frame.
                _visible = false;
                return;
            }

            if (_picker.BoardWidth <= 0 || _picker.BoardHeight <= 0)
            {
                ReleaseLabels();
                return;
            }

            Rebuild();
        }

        private void Rebuild()
        {
            ReleaseLabels();

            _width = _picker.BoardWidth;
            _height = _picker.BoardHeight;
            _camera = ResolveCamera();

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Text label = _pool.Rent();
                    label.transform.SetParent(_labelRoot, false);
                    label.text = Caption(x, y);
                    _active.Add(label);
                }
            }

            Reposition();
        }

        private void Reposition()
        {
            int index = 0;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (index >= _active.Count)
                    {
                        return;
                    }

                    _active[index].rectTransform.anchoredPosition = ToLocal(new GridPos(x, y));
                    index++;
                }
            }

            CacheProbe();
        }

        private Vector2 ToLocal(GridPos cell)
        {
            Vector2 screen = _picker.CellToScreen(cell);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelRoot, screen, _camera, out Vector2 local);
            return local;
        }

        private string Caption(int x, int y)
        {
            _text.Length = 0;
            _text.Append('(').Append(x).Append(", ").Append(y).Append(')');
            return _text.ToString();
        }

        private bool EnsurePool()
        {
            if (_pool != null)
            {
                return true;
            }

            if (_labelPrefab == null || _labelRoot == null)
            {
                return false;
            }

            _pool = new CheatLabelPool(_labelPrefab, _labelRoot, Mathf.Max(0, _labelPrewarm));
            return true;
        }

        private void ReleaseLabels()
        {
            if (_pool != null)
            {
                for (int i = 0; i < _active.Count; i++)
                {
                    _pool.Release(_active[i]);
                }
            }

            _active.Clear();
            _width = 0;
            _height = 0;
        }

        private bool HasBoardMoved()
        {
            Vector2 min = _picker.CellToScreen(new GridPos(0, 0));
            Vector2 max = _picker.CellToScreen(new GridPos(_width - 1, _height - 1));

            return (min - _probeMin).sqrMagnitude > ProbeEpsilon * ProbeEpsilon
                   || (max - _probeMax).sqrMagnitude > ProbeEpsilon * ProbeEpsilon;
        }

        private void CacheProbe()
        {
            _probeMin = _picker.CellToScreen(new GridPos(0, 0));
            _probeMax = _picker.CellToScreen(new GridPos(_width - 1, _height - 1));
        }

        /// <summary>Overlay canvases convert with a null camera; anything else needs its own.</summary>
        private Camera ResolveCamera()
        {
            Canvas canvas = _labelRoot != null ? _labelRoot.GetComponentInParent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }
    }
}
