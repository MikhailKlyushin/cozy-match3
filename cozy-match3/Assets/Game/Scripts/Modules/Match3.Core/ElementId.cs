using System;

namespace Match3.Core
{
    /// <summary>
    /// Handle into the obstacle catalogue. Obstacles are data on the §7.1 axes rather than an
    /// enum of types (A04), so the model stores a catalogue id, not an obstacle kind.
    /// </summary>
    public readonly struct ElementId : IEquatable<ElementId>
    {
        public static readonly ElementId None = default;

        public readonly ushort Value;

        public ElementId(ushort value)
        {
            Value = value;
        }

        public bool IsNone => Value == 0;

        public bool Equals(ElementId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ElementId other && Value == other.Value;

        public override int GetHashCode() => Value;

        public static bool operator ==(ElementId a, ElementId b) => a.Value == b.Value;

        public static bool operator !=(ElementId a, ElementId b) => a.Value != b.Value;

        /// <summary>Allocates: logs and tests only (§14).</summary>
        public override string ToString() => Value.ToString();
    }
}
