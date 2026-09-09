namespace Match3.Resolve
{
    public enum TurnOutcome : byte
    {
        /// <summary>Rejected at VALIDATE: no move charged, board untouched (E16).</summary>
        Rejected = 0,

        Resolved = 1,

        /// <summary>Takes precedence over <see cref="Lost"/> (D14, E13, E14).</summary>
        Won = 2,

        Lost = 3
    }
}
