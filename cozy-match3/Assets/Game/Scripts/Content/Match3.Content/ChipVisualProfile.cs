using System;
using Match3.Core;
using UnityEngine;

namespace Match3.Content
{
    /// <summary>
    /// Chip colour index to sprite, particle colour and destruction FX. The only place where the
    /// visual style exists: the domain knows colours as indices and nothing else (A12).
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Chip Visual Profile", fileName = "ChipVisualProfile")]
    public sealed class ChipVisualProfile : ScriptableObject
    {
        [SerializeField] private ChipVisual[] _chips = Array.Empty<ChipVisual>();
        [SerializeField] private BoosterVisual[] _boosters = Array.Empty<BoosterVisual>();

        public Sprite GetChipSprite(ChipColor color)
        {
            ChipVisual visual = FindChip(color);
            return visual != null ? visual.Sprite : null;
        }

        public Color GetParticleColor(ChipColor color)
        {
            ChipVisual visual = FindChip(color);
            return visual != null ? visual.ParticleColor : Color.white;
        }

        public GameObject GetDestroyFx(ChipColor color)
        {
            ChipVisual visual = FindChip(color);
            return visual != null ? visual.DestroyFx : null;
        }

        public Sprite GetBoosterSprite(BoosterType booster)
        {
            for (int i = 0; i < _boosters.Length; i++)
            {
                if (_boosters[i] != null && _boosters[i].Booster == booster)
                {
                    return _boosters[i].Sprite;
                }
            }

            return null;
        }

        /// <summary>Heads and flares: the effect a booster shows where it sits.</summary>
        public GameObject GetBoosterFx(BoosterType booster)
        {
            BoosterVisual visual = FindBooster(booster);
            return visual != null ? visual.ActivationFx : null;
        }

        /// <summary>
        /// Trails and rays - anything stretched into a line. Falls back to the activation effect,
        /// so a profile filled in before the roles existed plays exactly as it did.
        /// </summary>
        public GameObject GetBoosterBeamFx(BoosterType booster)
        {
            BoosterVisual visual = FindBooster(booster);
            return visual != null ? visual.BeamFx : null;
        }

        /// <summary>Shockwaves - anything that expands from a point.</summary>
        public GameObject GetBoosterBurstFx(BoosterType booster)
        {
            BoosterVisual visual = FindBooster(booster);
            return visual != null ? visual.BurstFx : null;
        }

        /// <summary>The hit at the far end of a flight.</summary>
        public GameObject GetBoosterImpactFx(BoosterType booster)
        {
            BoosterVisual visual = FindBooster(booster);
            return visual != null ? visual.ImpactFx : null;
        }

        private BoosterVisual FindBooster(BoosterType booster)
        {
            for (int i = 0; i < _boosters.Length; i++)
            {
                if (_boosters[i] != null && _boosters[i].Booster == booster)
                {
                    return _boosters[i];
                }
            }

            return null;
        }

        private ChipVisual FindChip(ChipColor color)
        {
            for (int i = 0; i < _chips.Length; i++)
            {
                if (_chips[i] != null && _chips[i].Color == color)
                {
                    return _chips[i];
                }
            }

            return null;
        }

        [Serializable]
        public sealed class ChipVisual
        {
            [SerializeField] private ChipColor _color = ChipColor.C1;
            [SerializeField] private Sprite _sprite;

            // Qualified: the Color property below shadows UnityEngine.Color in this scope.
            [SerializeField] private Color _particleColor = UnityEngine.Color.white;

            [SerializeField] private GameObject _destroyFx;

            public ChipColor Color => _color;

            public Sprite Sprite => _sprite;

            public Color ParticleColor => _particleColor;

            public GameObject DestroyFx => _destroyFx;
        }

        [Serializable]
        public sealed class BoosterVisual
        {
            [SerializeField] private BoosterType _booster = BoosterType.RocketH;
            [SerializeField] private Sprite _sprite;
            [SerializeField] private GameObject _activationFx;
            [SerializeField] private GameObject _beamFx;
            [SerializeField] private GameObject _burstFx;
            [SerializeField] private GameObject _impactFx;

            public BoosterType Booster => _booster;

            public Sprite Sprite => _sprite;

            public GameObject ActivationFx => _activationFx;

            /// <summary>
            /// Role fields are optional: an empty one falls back to the activation effect, which
            /// is what every booster used for every role before the roles were split (T31).
            /// </summary>
            public GameObject BeamFx => _beamFx != null ? _beamFx : _activationFx;

            public GameObject BurstFx => _burstFx != null ? _burstFx : _activationFx;

            public GameObject ImpactFx => _impactFx != null ? _impactFx : _activationFx;
        }
    }
}
