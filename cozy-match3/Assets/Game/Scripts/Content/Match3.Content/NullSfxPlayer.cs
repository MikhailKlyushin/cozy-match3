using System.Threading;

namespace Match3.Content
{
    /// <summary>
    /// The player bound when the scene carries no audio profile. Missing audio content is a gap,
    /// not a broken invariant: the level still plays, so every caller can hold a non-null player
    /// and none of them has to ask whether sound exists.
    /// </summary>
    public sealed class NullSfxPlayer : ISfxPlayer
    {
        public void Play(SfxId id)
        {
        }

        public void Play(SfxId id, float pitchScale, float delay, CancellationToken ct)
        {
        }

        public void PlayLadder(SfxId id, int rung)
        {
        }
    }
}
