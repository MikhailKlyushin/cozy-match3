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

        public GameObject GetBoosterFx(BoosterType booster)
        {
            for (int i = 0; i < _boosters.Length; i++)
            {
                if (_boosters[i] != null && _boosters[i].Booster == booster)
                {
                    return _boosters[i].ActivationFx;
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

            public BoosterType Booster => _booster;

            public Sprite Sprite => _sprite;

            public GameObject ActivationFx => _activationFx;
        }
    }
}
