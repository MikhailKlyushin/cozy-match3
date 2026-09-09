using System.Collections.Generic;
using Match3.Board;
using Match3.Core;
using Match3.Resolve;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Support
{
    /// <summary>
    /// A board of chip identities rebuilt from nothing but the transcript, the way BoardView does
    /// it (rule V1): the board is read once at construction, everything after that comes from
    /// events. Comparing it with the model after a turn is what proves the transcript never leaves
    /// the view owning a chip the board dropped - the defect that reaches the screen as two chips
    /// drawn in one cell.
    /// </summary>
    public sealed class TranscriptViewSimulator
    {
        private readonly Dictionary<int, GridPos> _cellByInstance = new Dictionary<int, GridPos>(128);
        private readonly Dictionary<GridPos, int> _instanceByCell = new Dictionary<GridPos, int>(128);
        private readonly List<string> _violations = new List<string>();

        public TranscriptViewSimulator(BoardModel board)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    ChipSlot slot = board.GetSlot(cell);
                    if (!slot.IsEmpty)
                    {
                        _cellByInstance[slot.InstanceId] = cell;
                    }
                }
            }
        }

        /// <summary>
        /// What the view could not make sense of: an identity spawned twice, or an event naming an
        /// identity it never saw. Both mean a slot changed hands without the transcript saying so.
        /// </summary>
        public IReadOnlyList<string> Violations => _violations;

        public void Apply(TurnTranscript transcript)
        {
            for (int i = 0; i < transcript.EventCount; i++)
            {
                ref readonly TurnEvent e = ref transcript.GetEvent(i);

                switch (e.Kind)
                {
                    case TurnEventKind.ChipSpawned:
                        Spawn(e.InstanceId, e.A);
                        break;

                    // A booster placed over a live chip takes that chip's identity and morphs in
                    // place (§6.3); only an id the view does not know is a new view.
                    case TurnEventKind.BoosterSpawned:
                    case TurnEventKind.MovesBonusRocket:
                        if (e.InstanceId != 0 && !_cellByInstance.ContainsKey(e.InstanceId))
                        {
                            Spawn(e.InstanceId, e.A);
                        }

                        break;

                    case TurnEventKind.ChipDestroyed:
                        Destroy(e.InstanceId);
                        break;

                    case TurnEventKind.ChipMoved:
                        MoveTo(e.InstanceId, e.B);
                        break;

                    case TurnEventKind.SwapPerformed:
                        Swap(e.A, e.B);
                        break;
                }
            }
        }

        /// <summary>
        /// True when the view and the board disagree: a cell claimed by two identities, a cell
        /// whose occupant differs, or a complaint collected while applying the events.
        /// </summary>
        public bool TryFindMismatch(BoardModel board, out string message)
        {
            if (_violations.Count > 0)
            {
                message = _violations[0];
                return true;
            }

            _instanceByCell.Clear();
            foreach (KeyValuePair<int, GridPos> pair in _cellByInstance)
            {
                if (_instanceByCell.TryGetValue(pair.Value, out int other))
                {
                    message = "cell " + pair.Value.ToString() + " is claimed by two chips at once: "
                              + other.ToString() + " and " + pair.Key.ToString();
                    return true;
                }

                _instanceByCell[pair.Value] = pair.Key;
            }

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    ChipSlot slot = board.GetSlot(cell);
                    int expected = slot.IsEmpty ? 0 : slot.InstanceId;
                    _instanceByCell.TryGetValue(cell, out int actual);

                    if (expected != actual)
                    {
                        message = "cell " + cell.ToString() + ": the board holds chip "
                                  + expected.ToString() + ", the view holds " + actual.ToString();
                        return true;
                    }
                }
            }

            message = null;
            return false;
        }

        private void Spawn(int instanceId, GridPos cell)
        {
            if (instanceId == 0)
            {
                _violations.Add("a chip appeared at " + cell.ToString() + " without an identity");
                return;
            }

            if (_cellByInstance.TryGetValue(instanceId, out GridPos existing))
            {
                _violations.Add("chip " + instanceId.ToString() + " spawned at " + cell.ToString()
                                + " while the view still holds it at " + existing.ToString());
            }

            _cellByInstance[instanceId] = cell;
        }

        private void Destroy(int instanceId)
        {
            if (!_cellByInstance.Remove(instanceId))
            {
                _violations.Add("chip " + instanceId.ToString()
                                + " was destroyed, but the view never had it");
            }
        }

        private void MoveTo(int instanceId, GridPos cell)
        {
            if (!_cellByInstance.ContainsKey(instanceId))
            {
                _violations.Add("chip " + instanceId.ToString() + " moved to " + cell.ToString()
                                + ", but the view never had it");
                return;
            }

            _cellByInstance[instanceId] = cell;
        }

        private void Swap(GridPos a, GridPos b)
        {
            // A tap writes SwapPerformed(cell, cell) (D03): nothing travels.
            if (a == b)
            {
                return;
            }

            bool hasFirst = TryGetInstanceAt(a, out int first);
            bool hasSecond = TryGetInstanceAt(b, out int second);

            if (hasFirst)
            {
                _cellByInstance[first] = b;
            }

            if (hasSecond)
            {
                _cellByInstance[second] = a;
            }
        }

        private bool TryGetInstanceAt(GridPos cell, out int instanceId)
        {
            foreach (KeyValuePair<int, GridPos> pair in _cellByInstance)
            {
                if (pair.Value == cell)
                {
                    instanceId = pair.Key;
                    return true;
                }
            }

            instanceId = 0;
            return false;
        }
    }
}
