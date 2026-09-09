using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Resolve;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Plays one family of transcript events. A new event kind means a new player, never an edit
    /// to a central switch (A11).
    /// </summary>
    public interface ITurnEventPlayer
    {
        /// <summary>Event kinds this player handles. Two players may not claim the same kind.</summary>
        IReadOnlyList<TurnEventKind> Kinds { get; }

        UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct);
    }
}
