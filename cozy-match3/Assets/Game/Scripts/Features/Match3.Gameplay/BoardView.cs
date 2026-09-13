using System.Collections.Generic;
using Match3.Board;
using Match3.Content;
using Match3.Core;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Match3.Gameplay
{
    /// <summary>
    /// Root of the board's visuals: pools, the InstanceId to ChipView map and the initial build.
    /// Building the level is the ONLY place the view is allowed to read the board (rule V1);
    /// while a turn is replaying, every change comes from the transcript.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardCellPicker
    {
        [SerializeField] private RectTransform _boardRoot;
        [SerializeField] private RectTransform _cellLayer;
        [SerializeField] private RectTransform _elementLayer;
        [SerializeField] private RectTransform _chipLayer;
        [SerializeField] private RectTransform _fxLayer;
        [SerializeField] private ChipView _chipPrefab;
        [SerializeField] private ElementView _elementPrefab;
        [SerializeField] private Image _cellPrefab;
        [SerializeField] private int _chipPrewarm = 96;
        [SerializeField] private int _elementPrewarm = 32;

        private readonly Dictionary<int, ChipView> _chipsByInstance = new Dictionary<int, ChipView>(128);
        private readonly List<ChipView> _activeChips = new List<ChipView>(128);

        private ChipVisualProfile _chipProfile;
        private ElementVisualProfile _elementProfile;
        private IMatch3Logger _logger;
        private ViewPool<ChipView> _chipPool;
        private ViewPool<ElementView> _elementPool;
        private ViewPool<Image> _cellTilePool;
        private ElementView[] _elementViews;
        private Image[] _cellTiles;
        private int _width;
        private int _height;
        private int _colorCount;
        private Vector2 _lastAreaSize;

        public BoardLayout Layout { get; } = new BoardLayout();

        public RectTransform FxLayer => _fxLayer;

        public int BoardWidth => _width;

        public int BoardHeight => _height;

        [Inject]
        public void Construct(
            ChipVisualProfile chipProfile,
            ElementVisualProfile elementProfile,
            IMatch3Logger logger)
        {
            _chipProfile = chipProfile;
            _elementProfile = elementProfile;
            _logger = logger;
        }

        /// <summary>Builds the level's initial visuals. Reads the board exactly once (rule V1).</summary>
        public void Build(IBoardReader board, int colorCount)
        {
            Clear();

            _width = board.Width;
            _height = board.Height;
            _colorCount = colorCount;

            EnsurePools();

            // Elements draw over chips: a chip now drops through a blocker's cell (§5.3) and has
            // to pass behind it. Nothing in scope shares a cell with a chip otherwise, since every
            // element on the occupancy axis is OccupiesCell (§7.1).
            if (_elementLayer != null)
            {
                _elementLayer.SetAsLastSibling();
            }

            Layout.SetBoardSize(_width, _height);
            RefreshArea(force: true);

            _elementViews = new ElementView[_width * _height];
            _cellTiles = new Image[_width * _height];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (board.GetKind(cell) != CellKind.Playable)
                    {
                        continue;
                    }

                    CreateCellTile(cell);

                    if (board.TryGetElement(cell, out ElementInstance element))
                    {
                        CreateElementView(cell, element, board.Catalog);
                    }

                    ChipSlot slot = board.GetSlot(cell);
                    if (slot.Kind == SlotKind.Chip)
                    {
                        SpawnChipAt(slot.InstanceId, slot.Color, cell);
                    }
                    else if (slot.Kind == SlotKind.Booster)
                    {
                        SpawnBoosterAt(slot.InstanceId, slot.Booster, cell);
                    }
                }
            }
        }

        /// <summary>
        /// Recomputes the layout when the area changed and repositions everything. Called on
        /// resolution changes - cell size is never cached once (§13).
        /// </summary>
        public bool RefreshArea(bool force = false)
        {
            Vector2 area = _boardRoot != null ? _boardRoot.rect.size : Vector2.zero;
            bool changed = Layout.SetAreaSize(area);
            if (!changed && !force)
            {
                return false;
            }

            _lastAreaSize = area;
            RepositionAll();
            return true;
        }

        public bool TryGetChip(int instanceId, out ChipView chip) => _chipsByInstance.TryGetValue(instanceId, out chip);

        /// <summary>
        /// Looks a chip up by the cell it believes it occupies. Needed by the events that carry
        /// cells instead of ids - SwapPerformed and SwapRejected - and it stays inside rule V1
        /// because the cell comes from the view's own state, not from the board.
        /// </summary>
        public bool TryGetChipAt(GridPos cell, out ChipView chip)
        {
            for (int i = 0; i < _activeChips.Count; i++)
            {
                if (_activeChips[i].Cell == cell)
                {
                    chip = _activeChips[i];
                    return true;
                }
            }

            chip = null;
            return false;
        }

        public ChipView SpawnChipAt(int instanceId, ChipColor color, GridPos cell)
        {
            ReleaseChipAt(cell);

            ChipView chip = RentChip();
            chip.SetChip(instanceId, color, cell, Layout.CellCenter(cell), Layout.CellSize);
            Register(instanceId, chip);
            return chip;
        }

        public ChipView SpawnBoosterAt(int instanceId, BoosterType booster, GridPos cell)
        {
            ReleaseChipAt(cell);

            ChipView chip = RentChip();
            chip.SetBooster(instanceId, booster, cell, Layout.CellCenter(cell), Layout.CellSize);
            Register(instanceId, chip);
            return chip;
        }

        /// <summary>
        /// Spawns one chip above the top row, outside the mask, ready to fall into
        /// <paramref name="targetCell"/> (§11.3). REFILL releases one chip per column per pass, so
        /// a column that lost three cells spawns three times in one step;
        /// <paramref name="stackOffset"/> is how many of them are already queued above it, and
        /// without it they would all start from the same point and fan out on the way down.
        /// </summary>
        public ChipView SpawnChipAboveBoard(
            int instanceId,
            ChipColor color,
            GridPos targetCell,
            int stackOffset)
        {
            ReleaseChipAt(targetCell);

            ChipView chip = RentChip();
            var spawnCell = new GridPos(targetCell.X, _height + stackOffset);
            chip.SetChip(
                instanceId,
                color,
                spawnCell,
                Layout.SpawnPosition(targetCell.X, stackOffset),
                Layout.CellSize);
            Register(instanceId, chip);
            return chip;
        }

        public void DespawnChip(int instanceId)
        {
            if (_chipsByInstance.TryGetValue(instanceId, out ChipView chip))
            {
                ReleaseChip(chip);
            }
        }

        /// <summary>
        /// Despawns the view that just finished dying. The despawn is delayed by that animation,
        /// so by then the id can already belong to a newer view - releasing by id alone would
        /// remove the live chip and leave the dead one on the board.
        /// </summary>
        public void DespawnChip(int instanceId, ChipView expected)
        {
            if (expected == null)
            {
                DespawnChip(instanceId);
                return;
            }

            // A recycled view carries somebody else's identity by now, and killing it would erase
            // a live chip.
            if (expected.InstanceId != instanceId)
            {
                return;
            }

            if (_activeChips.Contains(expected))
            {
                ReleaseChip(expected);
            }
        }

        public ElementView GetElement(GridPos cell)
        {
            int index = Index(cell);
            return index >= 0 ? _elementViews[index] : null;
        }

        public void RemoveElement(GridPos cell)
        {
            int index = Index(cell);
            if (index < 0 || _elementViews[index] == null)
            {
                return;
            }

            ElementView view = _elementViews[index];
            _elementViews[index] = null;
            view.KillTweens();
            _elementPool.Release(view);
        }

        public ElementView RevealElement(GridPos cell, ElementInstance element, ElementCatalog catalog)
        {
            RemoveElement(cell);
            return CreateElementView(cell, element, catalog);
        }

        public void Clear()
        {
            for (int i = 0; i < _activeChips.Count; i++)
            {
                _activeChips[i].KillTweens();
                _chipPool?.Release(_activeChips[i]);
            }

            _activeChips.Clear();
            _chipsByInstance.Clear();

            if (_elementViews != null)
            {
                for (int i = 0; i < _elementViews.Length; i++)
                {
                    if (_elementViews[i] != null)
                    {
                        _elementViews[i].KillTweens();
                        _elementPool?.Release(_elementViews[i]);
                        _elementViews[i] = null;
                    }
                }
            }

            if (_cellTiles != null)
            {
                for (int i = 0; i < _cellTiles.Length; i++)
                {
                    if (_cellTiles[i] != null)
                    {
                        _cellTilePool?.Release(_cellTiles[i]);
                        _cellTiles[i] = null;
                    }
                }
            }
        }

        public GridPos PickCell(Vector2 screenPosition)
        {
            if (_chipLayer == null)
            {
                return GridPos.Invalid;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _chipLayer, screenPosition, null, out Vector2 local);
            return Layout.PositionToCell(local);
        }

        public Vector2 CellToScreen(GridPos cell)
        {
            Vector2 local = Layout.CellCenter(cell);
            Vector3 world = _chipLayer.TransformPoint(local);
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }

        private void Update()
        {
            // Cheap guard against a resolution or orientation change; the layout only recomputes
            // when the area actually differs.
            if (_boardRoot != null && _boardRoot.rect.size != _lastAreaSize)
            {
                RefreshArea();
            }
        }

        private void OnDestroy()
        {
            Clear();
            _chipPool?.Dispose();
            _elementPool?.Dispose();
            _cellTilePool?.Dispose();
        }

        private void EnsurePools()
        {
            _chipPool = _chipPool ?? new ViewPool<ChipView>(_chipPrefab, _chipLayer, _chipPrewarm);
            _elementPool = _elementPool ?? new ViewPool<ElementView>(_elementPrefab, _elementLayer, _elementPrewarm);
            _cellTilePool = _cellTilePool ?? new ViewPool<Image>(_cellPrefab, _cellLayer, _chipPrewarm);
        }

        private ChipView RentChip()
        {
            ChipView chip = _chipPool.Rent();
            chip.transform.SetParent(_chipLayer, false);
            chip.Initialise(_chipProfile);
            return chip;
        }

        private ElementView CreateElementView(GridPos cell, ElementInstance element, ElementCatalog catalog)
        {
            ElementDefinition definition = catalog.Get(element.Definition);
            ElementView view = _elementPool.Rent();
            view.transform.SetParent(_elementLayer, false);
            view.Initialise(_elementProfile, _chipProfile);
            view.Show(
                definition.Token,
                element.Health,
                definition.MaxHealth,
                element.CurrentColor,
                NextColorOf(definition, element),
                Layout.CellCenter(cell),
                Layout.CellSize);

            _elementViews[Index(cell)] = view;
            return view;
        }

        /// <summary>Only a cycling element previews its next colour (§7.2, Q5 = on).</summary>
        private ChipColor NextColorOf(ElementDefinition definition, ElementInstance element)
            => definition.PerTurn == PerTurnBehaviour.CycleColor
                ? ChipColors.NextCycling(element.CurrentColor, _colorCount)
                : ChipColor.None;

        private void CreateCellTile(GridPos cell)
        {
            Image tile = _cellTilePool.Rent();
            tile.transform.SetParent(_cellLayer, false);
            var rect = (RectTransform)tile.transform;
            rect.sizeDelta = new Vector2(Layout.CellSize, Layout.CellSize);
            rect.anchoredPosition = Layout.CellCenter(cell);
            tile.raycastTarget = false;
            _cellTiles[Index(cell)] = tile;
        }

        private void RepositionAll()
        {
            if (_cellTiles == null)
            {
                return;
            }

            float size = Layout.CellSize;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var cell = new GridPos(x, y);
                    int index = Index(cell);
                    Vector2 centre = Layout.CellCenter(cell);

                    if (_cellTiles[index] != null)
                    {
                        var rect = (RectTransform)_cellTiles[index].transform;
                        rect.sizeDelta = new Vector2(size, size);
                        rect.anchoredPosition = centre;
                    }

                    if (_elementViews[index] != null)
                    {
                        _elementViews[index].ApplyLayout(centre, size);
                    }
                }
            }

            for (int i = 0; i < _activeChips.Count; i++)
            {
                ChipView chip = _activeChips[i];
                // CellCenter extrapolates past the top row, which is where a chip waiting to fall
                // in sits, so the spawn stack survives a resolution change like everything else.
                chip.ApplyLayout(Layout.CellCenter(chip.Cell), size);
            }
        }

        private void Register(int instanceId, ChipView chip)
        {
            // Two views for one identity means the model replaced a slot without destroying the
            // id that left it. The stale one is dropped here: nothing would ever remove it, and it
            // would sit on the board overlapping whatever arrives next.
            if (_chipsByInstance.TryGetValue(instanceId, out ChipView existing) && existing != chip)
            {
                _logger?.Warn("Chip instance " + instanceId.ToString()
                              + " was registered twice; releasing the stale view");
                ReleaseChip(existing);
            }

            _chipsByInstance[instanceId] = chip;
            _activeChips.Add(chip);
        }

        /// <summary>
        /// Frees whatever view still claims <paramref name="cell"/> before something new lands
        /// there. One cell can hold one chip, so a second claimant is a leaked view and the pool
        /// would never get it back on its own.
        /// </summary>
        private void ReleaseChipAt(GridPos cell)
        {
            for (int i = _activeChips.Count - 1; i >= 0; i--)
            {
                ChipView chip = _activeChips[i];
                if (chip.Cell != cell)
                {
                    continue;
                }

                _logger?.Warn("Cell " + cell.ToString() + " still held chip instance "
                              + chip.InstanceId.ToString() + "; releasing the stale view");
                ReleaseChip(chip);
            }
        }

        private void ReleaseChip(ChipView chip)
        {
            // Only when the map still points here: a newer view may already own the id.
            if (_chipsByInstance.TryGetValue(chip.InstanceId, out ChipView mapped) && mapped == chip)
            {
                _chipsByInstance.Remove(chip.InstanceId);
            }

            _activeChips.Remove(chip);
            chip.KillTweens();
            _chipPool.Release(chip);
        }

        private int Index(GridPos cell)
        {
            if (_elementViews == null || cell.X < 0 || cell.X >= _width || cell.Y < 0 || cell.Y >= _height)
            {
                return -1;
            }

            return cell.Y * _width + cell.X;
        }
    }
}
