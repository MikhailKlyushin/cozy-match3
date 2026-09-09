namespace Match3.Core
{
    /// <summary>
    /// The four boosters of GDD §6.2, rocket split by orientation. Orientation comes from the
    /// rank-4 line, not from the swipe direction (§4.3).
    /// </summary>
    public enum BoosterType : byte
    {
        None = 0,

        /// <summary>Token rh: clears the whole row, obstacles do not stop it (D11).</summary>
        RocketH = 1,

        /// <summary>Token rv: clears the whole column (D11).</summary>
        RocketV = 2,

        /// <summary>Token bm: 5x5 square centred on its own cell, clipped by the board edge.</summary>
        Bomb = 3,

        /// <summary>Token rb: every chip of one colour (§6.2, E06).</summary>
        Rainbow = 4,

        /// <summary>Token pl: target cell from §6.1 plus its four orthogonal neighbours.</summary>
        Airplane = 5
    }

    public static class BoosterTypes
    {
        public static bool IsRocket(BoosterType type)
            => type == BoosterType.RocketH || type == BoosterType.RocketV;

        /// <summary>Combination matrix §6.3 does not depend on rocket orientation.</summary>
        public static BoosterType NormalizeForCombo(BoosterType type)
            => type == BoosterType.RocketV ? BoosterType.RocketH : type;
    }
}
