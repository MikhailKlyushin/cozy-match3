using System;
using System.Collections.Generic;
using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Resolve
{
    /// <summary>
    /// Proof of the §10 requirement: two made-up obstacles are added as catalogue data only, and
    /// DamageService and ClearService handle them without a single change.
    /// </summary>
    public sealed class ExtensibilityTests
    {
        /// <summary>Made-up obstacle: damaged only on its own cell, 2 hp, sits under a chip.</summary>
        private const string IceToken = "ic";

        /// <summary>Made-up obstacle: boosters only.</summary>
        private const string SteelToken = "st";

        [Test]
        public void IceObstacle_IsDamagedByAMatchOnItsOwnCell()
        {
            BoardModel board = IceBoard(out GridPos iceCell);
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewDamage(board);

            damage.ApplyMatchDamage(ChipColor.C1, RowCells(), sourceId: 0, writer);

            Assert.AreEqual(1, HealthAt(board, iceCell), "OnCell: the match covers the ice cell");
            Assert.AreEqual(1, CountDamage(transcript), "D07: one damage per source, however many cells it covers");
        }

        [Test]
        public void IceObstacle_IsNotDamagedByAnAdjacentMatch()
        {
            BoardModel board = BoardFixture.From(
                @"t1 t1 t1
                  .. ic ..",
                CatalogWithCustomObstacles());
            var iceCell = new GridPos(1, 0);
            var matchCells = new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1) };
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewDamage(board);

            damage.ApplyMatchDamage(ChipColor.C1, matchCells, sourceId: 0, writer);

            Assert.AreEqual(2, HealthAt(board, iceCell), "OnCell: an adjacent match must not touch it");
            Assert.AreEqual(0, CountDamage(transcript));
        }

        [Test]
        public void IceObstacle_IsDamagedByABoosterCoveringItsCell()
        {
            BoardModel board = IceBoard(out GridPos iceCell);
            TranscriptWriter writer = NewWriter(out _);
            DamageService damage = NewDamage(board);

            damage.ApplyBoosterDamage(RowCells(), sourceId: 0, writer);

            Assert.AreEqual(1, HealthAt(board, iceCell));
        }

        [Test]
        public void IceObstacle_IsClearedAtZeroHealthAndCreditsItsOwnGoal()
        {
            BoardModel board = IceBoard(out GridPos iceCell);
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(IceToken, 1) });
            var deltas = new PooledList<GoalDelta>();
            TranscriptWriter writer = NewWriter(out TurnTranscript transcript);
            DamageService damage = NewDamage(board);
            var clear = new ClearService(board, goals, NullLogger.Instance);

            damage.ApplyMatchDamage(ChipColor.C1, RowCells(), sourceId: 0, writer);
            clear.Clear(step: 0, ChipCells(board, RowCells()), writer, deltas);

            Assert.AreEqual(1, HealthAt(board, iceCell), "2 hp survives the first step");
            Assert.AreEqual(0, goals.GetState(0).Value, "§8.1: partial damage never counts");

            damage.BeginStep(1);
            damage.ApplyMatchDamage(ChipColor.C1, RowCells(), sourceId: 0, writer);
            clear.Clear(step: 1, ChipCells(board), writer, deltas);

            Assert.IsFalse(board.TryGetElement(iceCell, out _), "the ice leaves the cell at 0 hp");
            Assert.AreEqual(1, goals.GetState(0).Value);
            Assert.AreEqual(1, CountKind(transcript, TurnEventKind.ElementDestroyed));
        }

        [Test]
        public void SteelObstacle_IsDamagedByBoostersOnly()
        {
            BoardModel board = BoardFixture.From(
                @"t1
                  st",
                CatalogWithCustomObstacles());
            var steelCell = new GridPos(0, 0);
            var cells = new[] { new GridPos(0, 1) };
            TranscriptWriter writer = NewWriter(out _);
            DamageService damage = NewDamage(board);

            damage.ApplyMatchDamage(ChipColor.C1, cells, sourceId: 0, writer);
            Assert.AreEqual(1, HealthAt(board, steelCell), "BoosterOnly: a match does nothing");

            damage.ApplyBoosterDamage(cells, sourceId: 0, writer);

            Assert.AreEqual(0, HealthAt(board, steelCell), "BoosterOnly: the blast destroys it");
        }

        [Test]
        public void DefaultDamageRules_CoverEveryDamageSourceAxisValue()
        {
            IReadOnlyList<IDamageSourceRule> rules = DamageRules.CreateDefault();
            var axisValues = (DamageSourceKind[])Enum.GetValues(typeof(DamageSourceKind));

            for (int i = 0; i < axisValues.Length; i++)
            {
                bool covered = false;
                for (int r = 0; r < rules.Count; r++)
                {
                    if (rules[r].Kind == axisValues[i])
                    {
                        covered = true;
                    }
                }

                Assert.IsTrue(covered, "§10: a damageSource axis value without a rule silently disables damage");
            }
        }

        /// <summary>The built-in catalogue plus two obstacles that exist only in this test (§10).</summary>
        private static ElementCatalog CatalogWithCustomObstacles()
        {
            List<ElementDefinition> definitions = BuiltInElementCatalog.CreateDefinitions();

            definitions.Add(new ElementDefinition(
                NextId(definitions),
                IceToken,
                DamageSourceKind.OnCell,
                ElementColorMode.None,
                ChipColor.None,
                maxHealth: 2,
                Occupancy.UnderChip,
                GravityBehaviour.StaticPassable,
                SpreadBehaviour.None,
                GoalRole.Countable,
                PerTurnBehaviour.None,
                isColoredBox: false));

            definitions.Add(new ElementDefinition(
                NextId(definitions),
                SteelToken,
                DamageSourceKind.BoosterOnly,
                ElementColorMode.None,
                ChipColor.None,
                maxHealth: 1,
                Occupancy.OccupiesCell,
                GravityBehaviour.StaticBlocksFall,
                SpreadBehaviour.None,
                GoalRole.Countable,
                PerTurnBehaviour.None,
                isColoredBox: false));

            return new ElementCatalog(definitions);
        }

        private static ElementId NextId(List<ElementDefinition> definitions)
        {
            ushort max = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Id.Value > max)
                {
                    max = definitions[i].Id.Value;
                }
            }

            return new ElementId((ushort)(max + 1));
        }

        /// <summary>Row of three chips with the ice under the middle one (occupancy UnderChip).</summary>
        private static BoardModel IceBoard(out GridPos iceCell)
        {
            BoardModel board = BoardFixture.From("t1 ic t1", CatalogWithCustomObstacles());
            iceCell = new GridPos(1, 0);
            board.SetChip(iceCell, ChipColor.C1);
            return board;
        }

        private static GridPos[] RowCells()
            => new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) };

        private static CellBuffer ChipCells(BoardModel board, GridPos[] cells = null)
        {
            var buffer = new CellBuffer();
            buffer.Configure(board.Width, board.Height);
            if (cells == null)
            {
                return buffer;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                buffer.AddUnique(cells[i]);
            }

            return buffer;
        }

        private static TranscriptWriter NewWriter(out TurnTranscript transcript)
        {
            transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);
            writer.TurnBegin(seed: 1, movesLeft: 10);
            return writer;
        }

        private static DamageService NewDamage(BoardModel board)
        {
            var damage = new DamageService(board, DamageRules.CreateDefault(), NullLogger.Instance);
            damage.BeginStep(0);
            return damage;
        }

        private static int HealthAt(BoardModel board, GridPos cell)
        {
            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance element), "expected an element at the cell");
            return element.Health;
        }

        private static int CountDamage(TurnTranscript transcript)
            => CountKind(transcript, TurnEventKind.ElementDamaged);

        private static int CountKind(TurnTranscript transcript, TurnEventKind kind)
        {
            int count = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                if (transcript.GetEvent(i).Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
