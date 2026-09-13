using System;
using System.Collections.Generic;
using Match3.Board;
using Match3.Core;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Support
{
    /// <summary>
    /// Builds a board straight from a token grid so board tests need neither ScriptableObjects
    /// nor a scene. Rows are written top-down exactly like a level layout, so the first line is
    /// y = height - 1 (D01), and the flip happens here once.
    /// </summary>
    public static class BoardFixture
    {
        public static BoardModel From(string layout, ElementCatalog catalog = null)
        {
            catalog = catalog ?? BuiltInElementCatalog.Create();
            List<string[]> rows = ParseRows(layout);

            int height = rows.Count;
            int width = rows[0].Length;
            var board = new BoardModel(width, height, catalog);

            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                string[] tokens = rows[row];
                if (tokens.Length != width)
                {
                    throw new ArgumentException("Row " + row + " has " + tokens.Length + " tokens, expected " + width);
                }

                for (int x = 0; x < width; x++)
                {
                    Apply(board, catalog, new GridPos(x, y), tokens[x]);
                }
            }

            board.RecomputeDefaultSpawners();
            return board;
        }

        /// <summary>Nests <paramref name="token"/> inside the element already at the cell.</summary>
        public static void Nest(BoardModel board, GridPos cell, string token)
        {
            if (!board.Catalog.TryResolve(token, out ElementDefinition definition))
            {
                throw new ArgumentException("Unknown element token: " + token, nameof(token));
            }

            if (!board.TryGetElement(cell, out ElementInstance outer))
            {
                throw new InvalidOperationException("No element at " + cell + " to nest into.");
            }

            ElementInstance deepest = outer;
            while (deepest.NestedIndex != ElementInstance.NoNested)
            {
                deepest = board.ElementAt(deepest.NestedIndex);
            }

            int index = board.AddElement(definition.Id, InitialColor(definition), ElementInstance.NoNested);
            deepest.NestedIndex = index;
        }

        private static List<string[]> ParseRows(string layout)
        {
            var rows = new List<string[]>();
            string[] lines = layout.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                rows.Add(line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }

            if (rows.Count == 0)
            {
                throw new ArgumentException("Layout is empty.", nameof(layout));
            }

            return rows;
        }

        private static void Apply(BoardModel board, ElementCatalog catalog, GridPos p, string token)
        {
            if (token == ElementTokens.Hole)
            {
                board.SetKind(p, CellKind.Hole);
                return;
            }

            board.SetKind(p, CellKind.Playable);

            if (token == ElementTokens.PlayableCell)
            {
                return;
            }

            if (token.Length == ElementTokens.TokenLength
                && token[0] == ElementTokens.FixedChipPrefix
                && char.IsDigit(token[1]))
            {
                board.SetChip(p, ChipColors.FromIndex(token[1] - '0'));
                return;
            }

            if (TryParseBooster(token, out BoosterType booster))
            {
                board.SetBooster(p, booster);
                return;
            }

            if (catalog.TryResolve(token, out ElementDefinition definition))
            {
                int index = board.AddElement(definition.Id, InitialColor(definition), ElementInstance.NoNested);
                board.AttachElement(p, index);
                return;
            }

            throw new ArgumentException("Unknown token: " + token);
        }

        private static ChipColor InitialColor(ElementDefinition definition)
        {
            switch (definition.ColorMode)
            {
                case ElementColorMode.Fixed:
                    return definition.FixedColor;
                case ElementColorMode.Cycling:
                    return ChipColor.C1;
                default:
                    return ChipColor.None;
            }
        }

        private static bool TryParseBooster(string token, out BoosterType booster)
        {
            switch (token)
            {
                case ElementTokens.RocketH:
                    booster = BoosterType.RocketH;
                    return true;
                case ElementTokens.RocketV:
                    booster = BoosterType.RocketV;
                    return true;
                case ElementTokens.Bomb:
                    booster = BoosterType.Bomb;
                    return true;
                case ElementTokens.Rainbow:
                    booster = BoosterType.Rainbow;
                    return true;
                case ElementTokens.Airplane:
                    booster = BoosterType.Airplane;
                    return true;
                default:
                    booster = BoosterType.None;
                    return false;
            }
        }
    }
}
