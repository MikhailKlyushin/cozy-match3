using System;
using UnityEngine;

namespace Match3.Content
{
    /// <summary>
    /// Obstacle token to sprites. Health stages are separate sprites because §7.1 requires the
    /// visual to change on every hit point lost; the bow and pip are white masks tinted at
    /// runtime, so cx works for any colorCount (§7.2).
    /// </summary>
    [CreateAssetMenu(menuName = "Match3/Element Visual Profile", fileName = "ElementVisualProfile")]
    public sealed class ElementVisualProfile : ScriptableObject
    {
        [SerializeField] private ElementVisual[] _elements = Array.Empty<ElementVisual>();
        [SerializeField] private Sprite _bowOverlay;
        [SerializeField] private Sprite _pipOverlay;

        /// <summary>Ribbon mask tinted with the box colour.</summary>
        public Sprite BowOverlay => _bowOverlay;

        /// <summary>Next-colour pip of the cx box (§7.2, Q5 = on).</summary>
        public Sprite PipOverlay => _pipOverlay;

        public bool TryGet(string token, out ElementVisual visual)
        {
            for (int i = 0; i < _elements.Length; i++)
            {
                if (_elements[i] != null && string.Equals(_elements[i].Token, token, StringComparison.Ordinal))
                {
                    visual = _elements[i];
                    return true;
                }
            }

            visual = null;
            return false;
        }

        [Serializable]
        public sealed class ElementVisual
        {
            [SerializeField] private string _token = string.Empty;

            [Tooltip("Index 0 is full health; the last entry is one hit from destruction.")]
            [SerializeField] private Sprite[] _healthStages = Array.Empty<Sprite>();

            [SerializeField] private bool _showBow;
            [SerializeField] private bool _showNextColorPip;
            [SerializeField] private GameObject _destroyFx;

            public string Token => _token;

            public bool ShowBow => _showBow;

            public bool ShowNextColorPip => _showNextColorPip;

            public GameObject DestroyFx => _destroyFx;

            /// <summary>
            /// Sprite for the current health. Damage progresses through the stage list, so a
            /// 3 hp box looks different after every hit.
            /// </summary>
            public Sprite SpriteForHealth(int currentHealth, int maxHealth)
            {
                if (_healthStages.Length == 0)
                {
                    return null;
                }

                if (maxHealth <= 1 || _healthStages.Length == 1)
                {
                    return _healthStages[0];
                }

                int damage = Mathf.Clamp(maxHealth - currentHealth, 0, _healthStages.Length - 1);
                return _healthStages[damage];
            }
        }
    }
}
