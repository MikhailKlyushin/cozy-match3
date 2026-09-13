using Match3.Core;
using UnityEngine;

namespace Match3.Gameplay
{
    /// <summary>
    /// Maps board coordinates to canvas positions. Cell size is min(area/width, area/height) over
    /// the square board area (§13), recomputed whenever that area changes - caching it once is the
    /// bug that breaks every aspect ratio but the reference one.
    /// </summary>
    public sealed class BoardLayout
    {
        private int _width;
        private int _height;
        private Vector2 _areaSize;

        public float CellSize { get; private set; }

        /// <summary>Board extent in canvas units; centred inside the area.</summary>
        public Vector2 BoardSize => new Vector2(CellSize * _width, CellSize * _height);

        public bool IsConfigured => _width > 0 && _height > 0 && CellSize > 0f;

        public void SetBoardSize(int width, int height)
        {
            _width = width;
            _height = height;
            Recompute();
        }

        /// <summary>Call on every resolution or area change; cheap and idempotent.</summary>
        public bool SetAreaSize(Vector2 areaSize)
        {
            if (Mathf.Approximately(areaSize.x, _areaSize.x) && Mathf.Approximately(areaSize.y, _areaSize.y))
            {
                return false;
            }

            _areaSize = areaSize;
            Recompute();
            return true;
        }

        /// <summary>
        /// Cell centre in canvas units relative to the board's own centre. y grows upwards, so no
        /// flip happens here: the layout row order was already resolved by the parser (D01).
        /// </summary>
        public Vector2 CellCenter(GridPos p)
        {
            float originX = -(_width - 1) * 0.5f * CellSize;
            float originY = -(_height - 1) * 0.5f * CellSize;
            return new Vector2(originX + p.X * CellSize, originY + p.Y * CellSize);
        }

        /// <summary>
        /// Spawn position outside the board mask (§11.3): one cell above the top row, plus one row
        /// per chip the column already queued this step, so a column refilling three cells releases
        /// a solid stack instead of three chips from a single point.
        /// </summary>
        public Vector2 SpawnPosition(int x, int rowsAbove)
            => CellCenter(new GridPos(x, _height + rowsAbove));

        /// <summary>Inverse of <see cref="CellCenter"/>; used by input and the cheat cell picker.</summary>
        public GridPos PositionToCell(Vector2 local)
        {
            if (CellSize <= 0f)
            {
                return GridPos.Invalid;
            }

            float originX = -(_width - 1) * 0.5f * CellSize;
            float originY = -(_height - 1) * 0.5f * CellSize;

            int x = Mathf.RoundToInt((local.x - originX) / CellSize);
            int y = Mathf.RoundToInt((local.y - originY) / CellSize);

            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return GridPos.Invalid;
            }

            return new GridPos(x, y);
        }

        private void Recompute()
        {
            if (_width <= 0 || _height <= 0)
            {
                CellSize = 0f;
                return;
            }

            // The area is squared first so the board keeps its aspect across 9:16 .. 16:9 (§13).
            float side = Mathf.Min(_areaSize.x, _areaSize.y);
            CellSize = Mathf.Min(side / _width, side / _height);
        }
    }
}
