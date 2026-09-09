using System;

namespace Match3.Levels
{
    /// <summary>
    /// One entry of the config <c>contents</c> list (GDD §10.2). Nesting is never written in the
    /// grid: the grid gives the outer element, this list the inner ones, outer to inner.
    /// </summary>
    public readonly struct NestedContent
    {
        public readonly int X;
        public readonly int Y;

        /// <summary>Two-character element token (GDD §10.2).</summary>
        public readonly string Token;

        public NestedContent(int x, int y, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Nested content needs an element token.", nameof(token));
            }

            X = x;
            Y = y;
            Token = token;
        }
    }
}
