using System;
using Match3.Core;
using Match3.Gameplay.Playback;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Zenject;

namespace Match3.Gameplay
{
    public enum BoardInputKind : byte
    {
        /// <summary>Tap on a cell; a booster there activates in place (D03).</summary>
        Tap = 0,

        /// <summary>Drag onto an orthogonal neighbour.</summary>
        Swap = 1
    }

    /// <summary>One resolved player action. The flow layer turns it into a TurnRule call (T23).</summary>
    public readonly struct BoardInputIntent
    {
        public readonly BoardInputKind Kind;
        public readonly GridPos A;

        /// <summary><see cref="GridPos.Invalid"/> for a tap.</summary>
        public readonly GridPos B;

        public BoardInputIntent(BoardInputKind kind, GridPos a, GridPos b)
        {
            Kind = kind;
            A = a;
            B = b;
        }

        public static BoardInputIntent Tap(GridPos cell)
            => new BoardInputIntent(BoardInputKind.Tap, cell, GridPos.Invalid);

        public static BoardInputIntent Swap(GridPos a, GridPos b)
            => new BoardInputIntent(BoardInputKind.Swap, a, b);
    }

    /// <summary>
    /// Level-attempt state the input and hint presenters read. Declared here because
    /// Match3.Gameplay does not reference Match3.Progression (§3.1): the provider declares the
    /// contract, exactly as with <see cref="IBoardCellPicker"/>.
    /// </summary>
    public interface ILevelInputState
    {
        /// <summary>True while a popup, the cheat panel or an end-of-level sequence owns input.</summary>
        bool IsInputBlocked { get; }

        int MovesLeft { get; }

        /// <summary>0 disables the idle hint for the level (§5.5).</summary>
        float HintDelaySeconds { get; }
    }

    /// <summary>Pure swipe geometry, static so it is covered by edit-mode tests without a scene (§19).</summary>
    public static class SwipeDirection
    {
        /// <summary>
        /// A drag shorter than the threshold is a tap (D03); a longer one resolves to the dominant
        /// axis, so a diagonal drag still yields exactly one orthogonal neighbour.
        /// </summary>
        public static bool TryResolveSwap(GridPos origin, Vector2 delta, float thresholdPixels, out GridPos target)
        {
            target = GridPos.Invalid;

            if (!origin.IsValid)
            {
                return false;
            }

            float sqrLength = delta.sqrMagnitude;
            if (sqrLength <= 0f || sqrLength < thresholdPixels * thresholdPixels)
            {
                return false;
            }

            target = Neighbour(origin, delta);
            return true;
        }

        /// <summary>Dominant axis wins; a 45-degree tie goes horizontal, as §5.5 orders its tie-breaks.</summary>
        public static GridPos Neighbour(GridPos origin, Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return delta.x > 0f ? origin.Right : origin.Left;
            }

            return delta.y > 0f ? origin.Up : origin.Down;
        }

        public static bool IsInsideBoard(GridPos cell, int width, int height)
            => cell.X >= 0 && cell.X < width && cell.Y >= 0 && cell.Y < height;
    }

    /// <summary>
    /// Drag-to-swap and tap-to-activate on the New Input System low-level API. While a turn
    /// replays the input is DROPPED, never buffered: a queue here desynchronises the visuals from
    /// the model (D05, E17). Intents leave as events; calling TurnRule is the flow layer's job.
    /// </summary>
    public sealed class BoardInputPresenter : MonoBehaviour
    {
        [Tooltip("Drag distance that becomes a swap, as a fraction of a cell's on-screen size.")]
        [SerializeField] [Range(0.1f, 1f)] private float _swipeThreshold = 0.35f;

        private IBoardCellPicker _picker;
        private TranscriptPlayer _player;
        private ILevelInputState _session;

        private GridPos _originCell = GridPos.Invalid;
        private Vector2 _pressPosition;
        private float _thresholdPixels;
        private bool _tracking;
        private bool _swapEmitted;

        /// <summary>False while a turn replays or a popup owns the screen (D05).</summary>
        public bool IsInputAllowed => _player != null && !_player.IsPlaying && !_session.IsInputBlocked;

        public event Action<BoardInputIntent> IntentDetected;

        /// <summary>Any press, legal or not - the idle hint timer resets on all of them (§5.5).</summary>
        public event Action Interacted;

        [Inject]
        public void Construct(IBoardCellPicker picker, TranscriptPlayer player, ILevelInputState session)
        {
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (_picker == null || pointer == null)
            {
                return;
            }

            if (!IsInputAllowed)
            {
                // Dropped, not buffered: the drag in progress is abandoned (D05, E17).
                _tracking = false;
                return;
            }

            ButtonControl press = pointer.press;
            Vector2 position = pointer.position.ReadValue();

            // Separate ifs, not else-if: a fast tap presses and releases in the same frame.
            if (press.wasPressedThisFrame)
            {
                OnPressed(position);
            }

            if (_tracking && press.isPressed)
            {
                OnDragged(position);
            }

            if (press.wasReleasedThisFrame)
            {
                OnReleased();
            }
        }

        private void OnPressed(Vector2 position)
        {
            // Fires even for a press outside the board: a player who is trying things does not
            // need a hint (§5.5).
            Interacted?.Invoke();

            _pressPosition = position;
            _originCell = _picker.PickCell(position);
            _tracking = _originCell.IsValid;
            _swapEmitted = false;
            _thresholdPixels = CellScreenSize() * _swipeThreshold;
        }

        private void OnDragged(Vector2 position)
        {
            if (_swapEmitted)
            {
                return;
            }

            if (!SwipeDirection.TryResolveSwap(_originCell, position - _pressPosition, _thresholdPixels, out GridPos target))
            {
                return;
            }

            // One swap per drag, whether or not it lands on the board.
            _swapEmitted = true;

            if (SwipeDirection.IsInsideBoard(target, _picker.BoardWidth, _picker.BoardHeight))
            {
                IntentDetected?.Invoke(BoardInputIntent.Swap(_originCell, target));
            }
        }

        private void OnReleased()
        {
            if (_tracking && !_swapEmitted)
            {
                // TurnRule rejects a cell without a booster, so the view never has to read the
                // board to tell (rule V1, D03).
                IntentDetected?.Invoke(BoardInputIntent.Tap(_originCell));
            }

            _tracking = false;
            _originCell = GridPos.Invalid;
        }

        /// <summary>
        /// Cell size in screen pixels, measured between two neighbours so the drag feels the same
        /// at every resolution - a hardcoded pixel distance breaks outside the reference aspect.
        /// </summary>
        private float CellScreenSize()
        {
            var origin = new GridPos(0, 0);

            if (_picker.BoardWidth > 1)
            {
                return Vector2.Distance(_picker.CellToScreen(origin), _picker.CellToScreen(new GridPos(1, 0)));
            }

            if (_picker.BoardHeight > 1)
            {
                return Vector2.Distance(_picker.CellToScreen(origin), _picker.CellToScreen(new GridPos(0, 1)));
            }

            return 0f;
        }
    }
}
