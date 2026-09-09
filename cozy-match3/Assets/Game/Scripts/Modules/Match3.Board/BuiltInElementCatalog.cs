using System.Collections.Generic;
using Match3.Core;

namespace Match3.Board
{
    /// <summary>
    /// The GDD §7.2 catalogue expressed as data. b2/b3 differ from bx only by MaxHealth and cx
    /// only by its per-turn axis, which is the check that the axes actually carry the mechanics.
    /// </summary>
    public static class BuiltInElementCatalog
    {
        public static ElementCatalog Create() => new ElementCatalog(CreateDefinitions());

        public static List<ElementDefinition> CreateDefinitions()
        {
            var definitions = new List<ElementDefinition>(10);
            ushort nextId = 1;

            definitions.Add(Box(new ElementId(nextId++), ElementTokens.Box1, health: 1));
            definitions.Add(Box(new ElementId(nextId++), ElementTokens.Box2, health: 2));
            definitions.Add(Box(new ElementId(nextId++), ElementTokens.Box3, health: 3));

            for (int colorIndex = 1; colorIndex <= ChipColors.MaxColorCount; colorIndex++)
            {
                definitions.Add(new ElementDefinition(
                    new ElementId(nextId++),
                    ElementTokens.ColoredBox(colorIndex),
                    DamageSourceKind.AdjacentMatchOfColor,
                    ElementColorMode.Fixed,
                    ChipColors.FromIndex(colorIndex),
                    maxHealth: 1,
                    Occupancy.OccupiesCell,
                    GravityBehaviour.StaticBlocksFall,
                    SpreadBehaviour.None,
                    GoalRole.Countable,
                    PerTurnBehaviour.None,
                    isColoredBox: true));
            }

            definitions.Add(new ElementDefinition(
                new ElementId(nextId++),
                ElementTokens.ColoredBoxCycling,
                DamageSourceKind.AdjacentMatchOfColor,
                ElementColorMode.Cycling,
                ChipColor.None,
                maxHealth: 1,
                Occupancy.OccupiesCell,
                GravityBehaviour.StaticBlocksFall,
                SpreadBehaviour.None,
                GoalRole.Countable,
                PerTurnBehaviour.CycleColor,
                isColoredBox: true));

            definitions.Add(new ElementDefinition(
                new ElementId(nextId),
                ElementTokens.Blocker,
                DamageSourceKind.None,
                ElementColorMode.None,
                ChipColor.None,
                ElementDefinition.InfiniteHealth,
                Occupancy.OccupiesCell,
                GravityBehaviour.StaticBlocksFall,
                SpreadBehaviour.None,
                GoalRole.NotCountable,
                PerTurnBehaviour.None,
                isColoredBox: false));

            return definitions;
        }

        private static ElementDefinition Box(ElementId id, string token, int health)
            => new ElementDefinition(
                id,
                token,
                DamageSourceKind.AdjacentMatch,
                ElementColorMode.None,
                ChipColor.None,
                health,
                Occupancy.OccupiesCell,
                GravityBehaviour.StaticBlocksFall,
                SpreadBehaviour.None,
                GoalRole.Countable,
                PerTurnBehaviour.None,
                isColoredBox: false);
    }
}
