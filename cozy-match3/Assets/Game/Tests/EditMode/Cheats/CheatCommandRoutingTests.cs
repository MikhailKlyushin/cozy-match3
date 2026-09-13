#if MATCH3_CHEATS
using System;
using Match3.Cheats;
using Match3.Core;
using Match3.Resolve;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Cheats
{
    /// <summary>
    /// The §15 split. TurnRule executes the board commands and rejects the two that recreate the
    /// attempt, so a mis-routed command is a cheat that silently does nothing.
    /// </summary>
    public sealed class CheatCommandRoutingTests
    {
        [Test]
        public void BoardCommands_GoToTurnRule()
        {
            Assert.AreEqual(CheatRoute.Board, CheatCommandRouting.Route(CheatCommand.WinLevel()));
            Assert.AreEqual(CheatRoute.Board, CheatCommandRouting.Route(CheatCommand.LoseLevel()));
            Assert.AreEqual(CheatRoute.Board, CheatCommandRouting.Route(CheatCommand.AddMoves(5)));
            Assert.AreEqual(CheatRoute.Board, CheatCommandRouting.Route(CheatCommand.FreeMoves(true)));
            Assert.AreEqual(
                CheatRoute.Board,
                CheatCommandRouting.Route(CheatCommand.PlaceBooster(new GridPos(1, 1), BoosterType.Bomb)));
        }

        [Test]
        public void GoToLevelAndSetSeed_GoToTheLevelFlow()
        {
            Assert.AreEqual(CheatRoute.LevelFlow, CheatCommandRouting.Route(CheatCommand.GoToLevel(7)));
            Assert.AreEqual(CheatRoute.LevelFlow, CheatCommandRouting.Route(CheatCommand.SetSeed(4242)));
        }

        [Test]
        public void RouteMatchesTheCommandVocabulary_ForEveryKind()
        {
            foreach (CheatCommandKind kind in Enum.GetValues(typeof(CheatCommandKind)))
            {
                var command = new CheatCommand(kind);
                CheatRoute route = CheatCommandRouting.Route(command);

                if (command.IsBoardCommand)
                {
                    Assert.AreEqual(CheatRoute.Board, route, kind.ToString());
                    continue;
                }

                Assert.AreNotEqual(CheatRoute.Board, route, kind.ToString() + " must not reach TurnRule");
            }
        }

        [Test]
        public void UnknownKind_HasNoExecutor()
        {
            Assert.AreEqual(CheatRoute.Unsupported, CheatCommandRouting.Route(default));
        }

        [Test]
        public void CommandsThatHandOverTheScreen_CloseThePanelFirst()
        {
            Assert.IsTrue(CheatCommandRouting.ClosesPanel(CheatCommand.WinLevel()));
            Assert.IsTrue(CheatCommandRouting.ClosesPanel(CheatCommand.LoseLevel()));
            Assert.IsTrue(CheatCommandRouting.ClosesPanel(CheatCommand.GoToLevel(3)));
            Assert.IsTrue(CheatCommandRouting.ClosesPanel(CheatCommand.SetSeed(3)));
        }

        [Test]
        public void CommandsPlayedWithThePanelOpen_LeaveItOpen()
        {
            Assert.IsFalse(CheatCommandRouting.ClosesPanel(CheatCommand.AddMoves(5)));
            Assert.IsFalse(CheatCommandRouting.ClosesPanel(CheatCommand.FreeMoves(true)));
            Assert.IsFalse(
                CheatCommandRouting.ClosesPanel(CheatCommand.PlaceBooster(new GridPos(0, 0), BoosterType.Rainbow)));
        }
    }
}
#endif
