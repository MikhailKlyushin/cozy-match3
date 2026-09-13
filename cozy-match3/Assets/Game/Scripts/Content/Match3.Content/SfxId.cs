namespace Match3.Content
{
    /// <summary>
    /// Every sound the game plays, one id per moment of GDD §11.3. The name doubles as the clip's
    /// file name (<c>SFX_&lt;id&gt;.ogg</c>), so authoring fills the profile from the folder
    /// without a lookup table to keep in sync.
    /// </summary>
    public enum SfxId : byte
    {
        None = 0,

        /// <summary>COMMIT, 0.15 s.</summary>
        Swap = 1,

        /// <summary>Illegal swap, out and back (E16).</summary>
        SwapRejected = 2,

        /// <summary>One chip dying. The whole cascade is built out of this one.</summary>
        ChipDestroyed = 3,

        /// <summary>Landing squash at the end of a fall.</summary>
        ChipLand = 4,

        /// <summary>A booster is marked, not fired (D06).</summary>
        BoosterSpawned = 5,

        Rocket = 6,
        Bomb = 7,

        /// <summary>The rainbow's own volley, played once where the beams leave.</summary>
        Rainbow = 8,

        /// <summary>A single ray of that volley; throttled, since a full board sends dozens.</summary>
        RainbowBeam = 9,

        Airplane = 10,

        /// <summary>Obstacle hit that leaves it standing.</summary>
        ElementDamaged = 11,

        ElementDestroyed = 12,

        /// <summary>One unit of a goal counter, 0.15 s apart.</summary>
        GoalTick = 13,

        LevelWon = 14,
        LevelLost = 15
    }
}
