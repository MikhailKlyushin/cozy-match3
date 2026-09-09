using System;
using Match3.Board;
using Match3.Gameplay;
using Match3.Resolve;
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

        public LevelViewBootstrap(BoardView boardView, IBoardReader board, LevelRules rules)
        {
            _boardView = boardView != null ? boardView : throw new ArgumentNullException(nameof(boardView));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>The only legal board read outside a turn: the initial layout (rule V1).</summary>
        public void Initialize() => _boardView.Build(_board, _rules.ColorCount);
    }
}
