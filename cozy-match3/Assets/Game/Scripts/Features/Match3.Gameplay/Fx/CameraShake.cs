using System;
using DG.Tweening;
using UnityEngine;

namespace Match3.Gameplay
{
    /// <summary>
    /// Impact shake for boosters (§11.3: 0.05 s / 4 px rocket, 0.12 s / 10 px bomb). The board is
    /// a uGUI element, so this offsets the board root instead of the camera and always puts it
    /// back on its rest position. It owns the tweens on that root.
    /// </summary>
    public sealed class CameraShake
    {
        private readonly BoardView _board;
        private readonly TweenCallback _restore;

        private RectTransform _root;
        private Vector2 _rest;
        private bool _resolved;
        private bool _shaking;

        private const int Vibrato = 12;
        private const float Elasticity = 0.4f;

        public CameraShake(BoardView board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _restore = RestoreRest;
        }

        /// <summary>Overrides the shaken root; by default the board view's own RectTransform.</summary>
        public void SetRoot(RectTransform root)
        {
            Reset();
            _root = root;
            _resolved = true;
        }

        /// <summary>Amplitude is in canvas units, along <paramref name="direction"/>.</summary>
        public void Shake(float duration, float amplitude, Vector2 direction)
        {
            RectTransform root = Root();
            if (root == null || duration <= 0f || amplitude <= 0f || direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            if (_shaking)
            {
                // A second impact starts from rest, never from the offset the first one left.
                KillShake();
                RestoreRest();
            }
            else
            {
                // Re-read at rest: the board root moves when the HUD reflows (§11.3 aspect range).
                _rest = root.anchoredPosition;
            }

            _shaking = true;

            root.DOPunchAnchorPos(direction.normalized * amplitude, duration, Vibrato, Elasticity)
                .SetLink(root.gameObject)
                .OnComplete(_restore);
        }

        /// <summary>Stops the shake and restores the rest position; used on level teardown.</summary>
        public void Reset()
        {
            KillShake();
            RestoreRest();
        }

        private RectTransform Root()
        {
            if (_resolved)
            {
                return _root;
            }

            _resolved = true;
            _root = _board.transform as RectTransform;
            return _root;
        }

        private void KillShake()
        {
            if (_root != null)
            {
                _root.DOKill();
            }
        }

        private void RestoreRest()
        {
            // Nothing was ever offset, so there is no rest position to trust yet.
            if (!_shaking)
            {
                return;
            }

            _shaking = false;

            // Null once the level was destroyed mid-shake: there is nothing left to restore.
            if (_root == null)
            {
                return;
            }

            _root.anchoredPosition = _rest;
        }
    }
}
