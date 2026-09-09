using Match3.Core;

namespace Match3.Board
{
    /// <summary>Live obstacle state; the immutable part lives in <see cref="ElementDefinition"/>.</summary>
    public sealed class ElementInstance
    {
        /// <summary><see cref="ImmuneUntilStep"/> value meaning "not immune".</summary>
        public const int NotImmune = -1;

        /// <summary><see cref="NestedIndex"/> value meaning "contains nothing".</summary>
        public const int NoNested = -1;

        public ElementInstance(ElementId definition, int health, ChipColor currentColor, int nestedIndex)
        {
            Definition = definition;
            Health = health;
            CurrentColor = currentColor;
            NestedIndex = nestedIndex;
            ImmuneUntilStep = NotImmune;
        }

        public ElementId Definition { get; }

        /// <summary><see cref="ElementDefinition.InfiniteHealth"/> for the blocker.</summary>
        public int Health { get; internal set; }

        /// <summary>Damage uses the colour as of the DAMAGE stage, never a mid-resolution value (§7.2).</summary>
        public ChipColor CurrentColor { get; internal set; }

        /// <summary>Element revealed when this one is destroyed, or <see cref="NoNested"/>.</summary>
        public int NestedIndex { get; internal set; }

        /// <summary>
        /// A revealed nested element is immune until the end of the step that uncovered it,
        /// otherwise one bomb unwraps the whole chain in a single step (§7.1).
        /// </summary>
        public int ImmuneUntilStep { get; internal set; }

        public bool IsAlive => Health != 0;

        public bool IsIndestructible => Health == ElementDefinition.InfiniteHealth;

        public bool IsImmuneAtStep(int step) => ImmuneUntilStep >= step;
    }
}
