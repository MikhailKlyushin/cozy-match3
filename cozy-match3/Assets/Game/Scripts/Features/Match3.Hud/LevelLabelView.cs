using System.Globalization;
using TMPro;
using UnityEngine;

namespace Match3.Hud
{
    /// <summary>
    /// Current level number, under the goals panel (§11.2). Display only: the id arrives from the
    /// attempt that has just started, the view never asks the flow which level is running.
    /// </summary>
    public sealed class LevelLabelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        public void SetLevel(int levelId)
        {
            if (_label == null)
            {
                return;
            }

            _label.text = string.Format(CultureInfo.InvariantCulture, HudStrings.LevelFormat, levelId);
        }

        private void Awake()
        {
            if (_label == null)
            {
                _label = GetComponent<TMP_Text>();
            }

            if (_label != null)
            {
                _label.raycastTarget = false;
            }
        }
    }
}
