using System.Threading;

namespace Match3.Content
{
    /// <summary>
    /// Fire-and-forget sound. Presentation asks for an id and never holds a voice: a cascade can
    /// ask for thirty sounds in one frame, and deciding which of them actually reach the mixer is
    /// the player's job, not the caller's.
    /// </summary>
    public interface ISfxPlayer
    {
        void Play(SfxId id);

        /// <summary>
        /// <paramref name="pitchScale"/> multiplies the clip's own pitch. <paramref name="delay"/>
        /// in seconds, for a sound that has to land with an animation that has not finished yet,
        /// such as a chip touching down; the token is the one that owns that animation, so a level
        /// torn down mid-fall does not sound its landings afterwards.
        /// </summary>
        void Play(SfxId id, float pitchScale, float delay, CancellationToken ct);

        /// <summary>
        /// Plays <paramref name="id"/> one rung up the pitch ladder per <paramref name="rung"/>,
        /// counted from 0 - a cascade climbs by its step, a goal counter by its tick. The ladder
        /// itself is content, so the caller passes the rung it is on and never the pitch.
        /// </summary>
        void PlayLadder(SfxId id, int rung);
    }
}
