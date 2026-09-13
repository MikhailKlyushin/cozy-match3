using System;
using System.Collections.Generic;
using Match3.Board;

namespace Match3.Levels
{
    /// <summary>
    /// Turns the text layout into a <see cref="TokenGrid"/>. A token is exactly two characters
    /// separated by spaces (§10.2) and the first text row is y = height - 1 (D01) — the row flip
    /// happens exactly once, here.
    /// </summary>
    public static class LayoutParser
    {
        private static readonly char[] Separators = { ' ', '\t' };

        public static TokenGrid Parse(IReadOnlyList<string> rows, int width, int height, ElementCatalog catalog)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Layout size must be positive, got " + width + "x" + height + ".", nameof(width));
            }

            if (rows.Count != height)
            {
                throw new ArgumentException(
                    "Layout has " + rows.Count + " rows, expected height " + height + ".",
                    nameof(rows));
            }

            var grid = new TokenGrid(width, height);

            for (int row = 0; row < height; row++)
            {
                // D01: the first text row is the top row of the board.
                int y = height - 1 - row;
                string line = rows[row];
                if (line == null)
                {
                    throw new ArgumentException(Where(row, y) + "row is null.", nameof(rows));
                }

                string[] tokens = line.Trim().Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != width)
                {
                    throw new ArgumentException(
                        Where(row, y) + "has " + tokens.Length + " tokens, expected width " + width + ".",
                        nameof(rows));
                }

                for (int x = 0; x < width; x++)
                {
                    string token = tokens[x];
                    if (token.Length != ElementTokens.TokenLength)
                    {
                        throw new ArgumentException(
                            Where(row, y) + "column " + x + ": token '" + token + "' is " + token.Length
                            + " characters, expected " + ElementTokens.TokenLength + ".",
                            nameof(rows));
                    }

                    if (!LevelTokens.IsKnown(token, catalog))
                    {
                        throw new ArgumentException(
                            Where(row, y) + "column " + x + ": unknown token '" + token + "'.",
                            nameof(rows));
                    }

                    grid.Set(x, y, token);
                }
            }

            return grid;
        }

        /// <summary>Names both the text row and the board row, so the message fits the config and the board.</summary>
        private static string Where(int row, int y) => "Layout row " + row + " (y = " + y + ") ";
    }
}
