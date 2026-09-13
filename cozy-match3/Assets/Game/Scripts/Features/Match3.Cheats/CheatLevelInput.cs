using System.Globalization;

namespace Match3.Cheats
{
    /// <summary>
    /// Parsing for the two typed fields of §12. Existence of a level is never decided here -
    /// that is <c>LevelFlowRule.CanGoToLevel</c>'s answer, because ids are catalog data and need
    /// not be contiguous (§8.3).
    /// </summary>
    public static class CheatLevelInput
    {
        /// <summary>True when the text is an integer; whether that level exists is the flow rule's call.</summary>
        public static bool TryParseLevelNumber(string text, out int levelId)
            => TryParseInteger(text, out levelId);

        /// <summary>Any int is a legal seed, negatives included (D12).</summary>
        public static bool TryParseSeed(string text, out int seed)
            => TryParseInteger(text, out seed);

        private static bool TryParseInteger(string text, out int value)
        {
            value = 0;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
