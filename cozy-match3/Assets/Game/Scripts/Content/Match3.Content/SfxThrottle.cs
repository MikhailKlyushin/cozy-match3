namespace Match3.Content
{
    /// <summary>
    /// Per-id gate in front of the voices. A cascade step kills every one of its chips in a single
    /// frame, so without this the same clip starts a dozen times at the same instant and the ear
    /// hears a click instead of chips; with it, one pop stands for the step (GDD §11.4).
    /// <para>
    /// Time is passed in rather than read, so the rule is arithmetic and testable without a scene.
    /// </para>
    /// </summary>
    public sealed class SfxThrottle
    {
        /// <summary>Earliest time each id may sound again; index is the id itself.</summary>
        private readonly float[] _readyAt = new float[IdCount];

        private const int IdCount = (int)SfxId.LevelLost + 1;

        /// <summary>
        /// True when <paramref name="id"/> may sound at <paramref name="startTime"/>, which also
        /// books it: the next <paramref name="minInterval"/> seconds are taken.
        /// <paramref name="startTime"/> is when the sound will be heard, not when it was asked
        /// for - landings are all requested inside one frame and land seconds apart.
        /// </summary>
        public bool TryTake(SfxId id, float startTime, float minInterval)
        {
            var index = (int)id;
            if (index <= 0 || index >= IdCount)
            {
                return false;
            }

            if (startTime < _readyAt[index])
            {
                return false;
            }

            _readyAt[index] = startTime + minInterval;
            return true;
        }
    }
}
