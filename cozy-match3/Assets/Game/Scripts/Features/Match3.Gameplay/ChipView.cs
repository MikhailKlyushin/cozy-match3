using DG.Tweening;
using Match3.Content;
using Match3.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Gameplay
{
    /// <summary>
    /// One chip or booster on screen. Display only: it never reads or mutates the board, and it
    /// is driven entirely by the transcript player (rule V1).
    /// </summary>
    public sealed class ChipView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private RectTransform _rect;

        private ChipVisualProfile _profile;

        /// <summary>Model-side identity the player maps events onto.</summary>
        public int InstanceId { get; private set; }

        /// <summary>
        /// Cell this view believes it occupies. Owned by the view, updated from the transcript -
        /// it is what lets a resolution change reposition chips without reading the board (V1).
        /// </summary>
        public GridPos Cell { get; private set; }

        public RectTransform Rect => _rect;

        public Image Image => _image;

        public void Initialise(ChipVisualProfile profile)
        {
            _profile = profile;
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }
        }

        public void SetChip(int instanceId, ChipColor color, GridPos cell, Vector2 anchoredPosition, float cellSize)
        {
            InstanceId = instanceId;
            Cell = cell;
            _image.sprite = _profile != null ? _profile.GetChipSprite(color) : null;
            _image.color = Color.white;
            _image.raycastTarget = false;
            ApplyTransform(anchoredPosition, cellSize);
        }

        public void SetBooster(int instanceId, BoosterType booster, GridPos cell, Vector2 anchoredPosition, float cellSize)
        {
            InstanceId = instanceId;
            Cell = cell;
            _image.sprite = _profile != null ? _profile.GetBoosterSprite(booster) : null;
            _image.color = Color.white;
            _image.raycastTarget = false;
            ApplyTransform(anchoredPosition, cellSize);
        }

        /// <summary>Morph in place for a mass transformation: identity is kept (§6.3).</summary>
        public void MorphToBooster(BoosterType booster)
        {
            _image.sprite = _profile != null ? _profile.GetBoosterSprite(booster) : null;
        }

        public void SetPosition(GridPos cell, Vector2 anchoredPosition)
        {
            Cell = cell;
            _rect.anchoredPosition = anchoredPosition;
        }

        /// <summary>Position without changing the logical cell: used mid-tween and while falling.</summary>
        public void SetRawPosition(Vector2 anchoredPosition) => _rect.anchoredPosition = anchoredPosition;

        /// <summary>Resize on a layout change, keeping the logical cell.</summary>
        public void ApplyLayout(Vector2 anchoredPosition, float cellSize)
        {
            _rect.sizeDelta = new Vector2(cellSize, cellSize);
            _rect.anchoredPosition = anchoredPosition;
        }

        /// <summary>Landing squash, 0.06 s by §11.3, scheduled to fire when the fall ends.</summary>
        public void PlayLandingSquash(float duration, float delay)
        {
            if (duration <= 0f)
            {
                return;
            }

            _rect.DOPunchScale(new Vector3(0.14f, -0.14f, 0f), duration, vibrato: 1, elasticity: 0f)
                .SetDelay(delay)
                .SetLink(gameObject);
        }

        public void ResetVisualState()
        {
            _rect.localScale = Vector3.one;
            Color color = _image.color;
            color.a = 1f;
            _image.color = color;
        }

        /// <summary>Kills any tween still bound to this view before it goes back to the pool.</summary>
        public void KillTweens()
        {
            _rect.DOKill();
            _image.DOKill();
        }

        private void ApplyTransform(Vector2 anchoredPosition, float cellSize)
        {
            _rect.sizeDelta = new Vector2(cellSize, cellSize);
            _rect.anchoredPosition = anchoredPosition;
            ResetVisualState();
        }
    }
}
