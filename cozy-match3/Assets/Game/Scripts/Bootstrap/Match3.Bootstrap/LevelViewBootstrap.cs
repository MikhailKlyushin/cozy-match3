using System;
using Match3.Board;
using Match3.Content;
using Match3.Core;
using Match3.Gameplay;
using Match3.Resolve;
using UnityEngine;
using Zenject;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Builds the board's visuals once the attempt's container has finished injecting. It cannot
    /// happen in the installer: BoardView's profiles arrive by injection, which runs after
    /// InstallBindings.
    /// </summary>
    public sealed class LevelViewBootstrap : IInitializable
    {
        private readonly BoardView _boardView;
        private readonly IBoardReader _board;
        private readonly LevelRules _rules;
        private readonly FxRegistry _fx;
        private readonly ChipVisualProfile _chipProfile;

        public LevelViewBootstrap(
            BoardView boardView,
            IBoardReader board,
            LevelRules rules,
            FxRegistry fx,
            ChipVisualProfile chipProfile)
        {
            _boardView = boardView != null ? boardView : throw new ArgumentNullException(nameof(boardView));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _fx = fx ?? throw new ArgumentNullException(nameof(fx));
            _chipProfile = chipProfile != null
                ? chipProfile
                : throw new ArgumentNullException(nameof(chipProfile));
        }

        /// <summary>The only legal board read outside a turn: the initial layout (rule V1).</summary>
        public void Initialize()
        {
            _boardView.Build(_board, _rules.ColorCount);
            PrewarmDestroyFx();
        }

        /// <summary>
        /// One destruction effect is rented per destroyed chip, and a rainbow-on-rainbow clears the
        /// whole board in a single step (§6.3). The pool's own default is eight, so without this the
        /// difference would be instantiated in the middle of a cascade - the frame drop §14 is
        /// about. Colours usually share one prefab, and then this costs exactly one board.
        /// </summary>
        private void PrewarmDestroyFx()
        {
            int cells = _board.Width * _board.Height;

            for (int colorIndex = 1; colorIndex <= _rules.ColorCount; colorIndex++)
            {
                GameObject prefab = _chipProfile.GetDestroyFx((ChipColor)colorIndex);
                if (prefab != null)
                {
                    _fx.Prewarm(prefab, cells);
                }
            }
        }
    }
}
