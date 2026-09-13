using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Content;
using Match3.Core;
using Match3.Resolve;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Replays a <see cref="TurnTranscript"/> event by event. The transcript is self-sufficient,
    /// so this never reads the board (rule V1), and input stays blocked while
    /// <see cref="IsPlaying"/> is true (D05, E17).
    /// </summary>
    public sealed class TranscriptPlayer
    {
        private readonly PlaybackContext _context;
        private readonly IMatch3Logger _logger;
        private readonly ITurnEventPlayer[] _byKind;

        private const int KindCount = (int)TurnEventKind.TurnEnd + 1;

        public TranscriptPlayer(
            BoardView board,
            TimingProfile timings,
            IReadOnlyList<ITurnEventPlayer> players,
            ISfxPlayer sfx,
            IMatch3Logger logger)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            _context = new PlaybackContext(board, timings, sfx ?? throw new ArgumentNullException(nameof(sfx)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _byKind = new ITurnEventPlayer[KindCount];

            for (int i = 0; i < players.Count; i++)
            {
                ITurnEventPlayer player = players[i];
                IReadOnlyList<TurnEventKind> kinds = player.Kinds;
                for (int k = 0; k < kinds.Count; k++)
                {
                    int index = (int)kinds[k];
                    if (_byKind[index] != null)
                    {
                        throw new InvalidOperationException(
                            "Two players registered for event kind " + kinds[k]);
                    }

                    _byKind[index] = player;
                }
            }
        }

        public bool IsPlaying { get; private set; }

        public async UniTask PlayAsync(TurnTranscript transcript, CancellationToken ct)
        {
            if (transcript == null)
            {
                throw new ArgumentNullException(nameof(transcript));
            }

            IsPlaying = true;
            _context.BeginTurn(transcript);

            try
            {
                for (int i = 0; i < transcript.EventCount; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    TurnEventKind kind = transcript.GetEvent(i).Kind;
                    ITurnEventPlayer player = _byKind[(int)kind];
                    if (player == null)
                    {
                        // Structural events with no visual (TurnBegin, StepBegin, GoalProgress for
                        // the HUD) legitimately have no player.
                        continue;
                    }

                    await player.PlayAsync(_context, i, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Level teardown mid-cascade: the caller owns the token, nothing to log.
                throw;
            }
            catch (Exception e)
            {
                // A silent failure here shows up as a frozen board with no explanation.
                _logger.Error("Transcript playback failed: " + e);
                throw;
            }
            finally
            {
                IsPlaying = false;
            }
        }
    }
}
