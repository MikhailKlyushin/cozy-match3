namespace Match3.Goals
{
    /// <summary>The four goal types of GDD §8.1.</summary>
    public enum GoalType : byte
    {
        /// <summary>Chips of one colour, destroyed by a match, a booster or a chain alike.</summary>
        CollectColor = 0,

        /// <summary>Obstacles of one token brought to hp = 0; partial damage does not count.</summary>
        DestroyElement = 1,

        /// <summary>Any coloured box (c1-c6, cx) brought to hp = 0.</summary>
        DestroyAnyColoredBox = 2,

        /// <summary>Booster activations, not creations (Q1).</summary>
        ActivateBooster = 3
    }
}
