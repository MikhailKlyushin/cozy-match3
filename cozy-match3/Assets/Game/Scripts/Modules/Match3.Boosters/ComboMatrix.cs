using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>The ten filled cells of the §6.3 matrix.</summary>
    internal enum ComboKind : byte
    {
        None = 0,

        /// <summary>Rocket + rocket: full row plus full column through the epicentre.</summary>
        Cross = 1,

        /// <summary>Rocket + bomb: rows y-1..y+1 and columns x-1..x+1 in full.</summary>
        ThickCross = 2,

        /// <summary>Bomb + bomb: one 7x7 blast.</summary>
        BigBlast = 3,

        /// <summary>Rocket + rainbow: every chip of the needed colour becomes a rocket.</summary>
        RocketRain = 4,

        /// <summary>Bomb + rainbow: every chip of the needed colour becomes a bomb.</summary>
        BombRain = 5,

        /// <summary>Rainbow + rainbow: every chip, plus 1 damage to every live obstacle.</summary>
        WholeBoard = 6,

        /// <summary>Rocket + airplane: two airplanes, a rocket firing at each impact.</summary>
        RocketDelivery = 7,

        /// <summary>Bomb + airplane: two airplanes, a 5x5 blast at each impact.</summary>
        BombDelivery = 8,

        /// <summary>Rainbow + airplane: chips of the needed colour become airplanes, cap 8.</summary>
        AirplaneRain = 9,

        /// <summary>Airplane + airplane: three airplanes to goal targets 1, 2 and 3.</summary>
        ThreeAirplanes = 10
    }

    /// <summary>Matrix §6.3 as data, for <see cref="ComboResolver"/> and for tests.</summary>
    public static class ComboMatrix
    {
        private const int Size = 4;

        private const int RocketIndex = 0;
        private const int BombIndex = 1;
        private const int RainbowIndex = 2;
        private const int AirplaneIndex = 3;

        /// <summary>Symmetric by construction: (A,B) and (B,A) read the same cell.</summary>
        private static readonly ComboKind[] Cells =
        {
            // rocket
            ComboKind.Cross, ComboKind.ThickCross, ComboKind.RocketRain, ComboKind.RocketDelivery,
            // bomb
            ComboKind.ThickCross, ComboKind.BigBlast, ComboKind.BombRain, ComboKind.BombDelivery,
            // rainbow
            ComboKind.RocketRain, ComboKind.BombRain, ComboKind.WholeBoard, ComboKind.AirplaneRain,
            // airplane
            ComboKind.RocketDelivery, ComboKind.BombDelivery, ComboKind.AirplaneRain, ComboKind.ThreeAirplanes
        };

        public static bool IsCovered(BoosterType a, BoosterType b) => Kind(a, b) != ComboKind.None;

        internal static ComboKind Kind(BoosterType a, BoosterType b)
        {
            int rowIndex = IndexOf(BoosterTypes.NormalizeForCombo(a));
            int columnIndex = IndexOf(BoosterTypes.NormalizeForCombo(b));
            if (rowIndex < 0 || columnIndex < 0)
            {
                return ComboKind.None;
            }

            return Cells[rowIndex * Size + columnIndex];
        }

        /// <summary>Rocket orientation is already normalised away (§6.3).</summary>
        private static int IndexOf(BoosterType normalized)
        {
            switch (normalized)
            {
                case BoosterType.RocketH:
                    return RocketIndex;
                case BoosterType.Bomb:
                    return BombIndex;
                case BoosterType.Rainbow:
                    return RainbowIndex;
                case BoosterType.Airplane:
                    return AirplaneIndex;
                default:
                    return -1;
            }
        }
    }
}
