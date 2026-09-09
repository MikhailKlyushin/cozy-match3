using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// One strategy of the <see cref="DamageSourceKind"/> axis (§7.1). A new axis value means a
    /// new rule in the registry, never a switch inside DAMAGE or CLEAR (§10).
    /// </summary>
    public interface IDamageSourceRule
    {
        DamageSourceKind Kind { get; }

        bool IsDamagedBy(in DamageContext ctx);
    }
}
