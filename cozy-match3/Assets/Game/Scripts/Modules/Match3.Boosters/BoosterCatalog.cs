using System;
using System.Collections.Generic;
using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>Booster type to effect, keyed by the enum value so lookup is never iterated (I4).</summary>
    public sealed class BoosterCatalog
    {
        private readonly IBoosterEffect[] _byType;

        public BoosterCatalog(IReadOnlyList<IBoosterEffect> effects)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            _byType = new IBoosterEffect[(int)BoosterType.Airplane + 1];

            for (int i = 0; i < effects.Count; i++)
            {
                IBoosterEffect effect = effects[i];
                if (effect == null)
                {
                    throw new ArgumentNullException(nameof(effects), "Booster effect is null.");
                }

                int index = (int)effect.Type;
                if (index <= 0 || index >= _byType.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(effects),
                        "Unsupported booster type: " + effect.Type);
                }

                if (_byType[index] != null)
                {
                    throw new ArgumentException("Duplicate effect for " + effect.Type, nameof(effects));
                }

                _byType[index] = effect;
            }
        }

        public IBoosterEffect Get(BoosterType type)
        {
            int index = (int)type;
            if (index <= 0 || index >= _byType.Length || _byType[index] == null)
            {
                throw new ArgumentOutOfRangeException(nameof(type), "No booster effect for " + type);
            }

            return _byType[index];
        }
    }
}
