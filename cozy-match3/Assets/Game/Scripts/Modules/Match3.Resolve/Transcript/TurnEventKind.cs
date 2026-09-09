namespace Match3.Resolve
{
    /// <summary>
    /// Transcript event kinds: the single model-to-presentation channel (§6, A01). Event order
    /// equals GDD §5.1 stage order (rule T1), and the transcript is self-sufficient — a turn can
    /// be drawn from it without ever reading Board (rules T5/V1).
    /// </summary>
    public enum TurnEventKind : byte
    {
        TurnBegin = 0,

        /// <summary>Illegal swap: bounce back, no move charged (E16).</summary>
        SwapRejected = 1,

        /// <summary>COMMIT stage. A is the source cell, B the target cell.</summary>
        SwapPerformed = 2,

        /// <summary>
        /// Written immediately after <see cref="SwapPerformed"/>, before any
        /// <see cref="StepBegin"/> (D04, E15, rule T6). Value carries the remaining moves.
        /// </summary>
        MoveCharged = 3,

        /// <summary>Step is the cascade step index, from 0.</summary>
        StepBegin = 4,

        /// <summary>
        /// Booster marked but not fired in the same step (D06, rule T2). A is the spawn cell
        /// (§4.3), InstanceId the visual identity the view must register.
        /// </summary>
        BoosterSpawned = 5,

        /// <summary>A is the cell, Wave the chain wave, Flags a <see cref="BoosterActivationSource"/>.</summary>
        BoosterActivated = 6,

        /// <summary>
        /// A is the epicentre, i.e. the swap target cell. Booster is participant A, Value holds
        /// participant B cast from BoosterType (§6.3).
        /// </summary>
        ComboActivated = 7,

        /// <summary>Cells hit by the current activation, as a slice into Cells. Drives FX only.</summary>
        BoosterEffectCells = 8,

        ChipDestroyed = 9,

        /// <summary>Mass transformation from a rainbow combination (§6.3).</summary>
        ChipTransformed = 10,

        /// <summary>Amount is the damage applied, Value the remaining hp. One per source (D07).</summary>
        ElementDamaged = 11,

        /// <summary>hp reached 0; only here does an obstacle count towards a goal.</summary>
        ElementDestroyed = 12,

        /// <summary>Nested element uncovered; immune until the end of the current step (§7.1).</summary>
        ElementRevealed = 13,

        /// <summary>cx colour change, POST-TURN step 1 (§7.2).</summary>
        ElementColorCycled = 14,

        /// <summary>
        /// InstanceId is the goal index, Amount the delta, Value the new clamped total (E18,
        /// rule T4) — the HUD ticks from the delta rather than recomputing state.
        /// </summary>
        GoalProgress = 15,

        /// <summary>
        /// ANIMATE barrier: destruction FX must finish before anything falls (GDD §5.1 stages
        /// 7-8, rule T1). Direct client requirement.
        /// </summary>
        AnimateBarrier = 16,

        /// <summary>A to B, Flags is a <see cref="ChipMoveFlags"/>.</summary>
        ChipMoved = 17,

        /// <summary>New chip from a spawner; starts one cell above the top row, outside the mask (§11.3).</summary>
        ChipSpawned = 18,

        /// <summary>Value is the cascade depth reached.</summary>
        StepEnd = 19,

        /// <summary>0.12 s pause between chain waves (§6.4, rule T3).</summary>
        WaveBarrier = 20,

        PostTurnBegin = 21,

        ShuffleBegin = 22,

        ShuffleEnd = 23,

        /// <summary>Leftover-move rocket (§8.4); capped, and cannot lose the level (E23).</summary>
        MovesBonusRocket = 24,

        /// <summary>Flags is a <see cref="LevelEndReason"/>.</summary>
        LevelWon = 25,

        /// <summary>Flags is a <see cref="LevelEndReason"/>.</summary>
        LevelLost = 26,

        /// <summary>Flags is a <see cref="CapKind"/>. Always a bug, never balance (E05, §14).</summary>
        CapHit = 27,

        TurnEnd = 28
    }
}
