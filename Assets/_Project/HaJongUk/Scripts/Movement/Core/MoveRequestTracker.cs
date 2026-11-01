using System;
using System.Collections.Generic;

namespace OpenSourceTeam6.Movement.Core
{
    // Owns request identity and terminal results. Navigation remains in the adapter.
    public sealed class MoveRequestTracker
    {
        public const int Capacity = 64;
        private readonly Queue<MoveOutcome> outcomes = new Queue<MoveOutcome>(Capacity);
        private long sequence;
        public long ActiveId { get; private set; }
        public MoveState State { get; private set; }
        public MoveFailure LastFailure { get; private set; }
        public int PendingCount => outcomes.Count;
        // Reserve one terminal slot for every accepted request, including replacement.
        public bool CanBegin => sequence < long.MaxValue && outcomes.Count + (ActiveId == 0 ? 1 : 2) <= Capacity;

        public MoveReceipt Begin(WorldPoint currentPosition)
        {
            if (!CanBegin) return new MoveReceipt(0, MoveFailure.NotReady);
            if (ActiveId != 0) Complete(MoveState.Cancelled, currentPosition, cancellation: CancelReason.Replaced);
            ActiveId = ++sequence;
            State = MoveState.Planning;
            LastFailure = MoveFailure.None;
            return new MoveReceipt(ActiveId);
        }

        public void SetPhase(MoveState state)
        {
            if (state != MoveState.Planning && state != MoveState.Moving && state != MoveState.Suspended)
                throw new ArgumentOutOfRangeException(nameof(state));
            if (ActiveId != 0) State = state;
        }

        public bool Cancel(long id, WorldPoint position, CancelReason reason = CancelReason.Requested)
        {
            if (id == 0 || id != ActiveId) return false;
            return Complete(MoveState.Cancelled, position, cancellation: reason);
        }

        public bool Complete(MoveState state, WorldPoint position,
            MoveFailure failure = MoveFailure.None, CancelReason cancellation = CancelReason.None)
        {
            if (state != MoveState.Arrived && state != MoveState.Failed && state != MoveState.Cancelled)
                throw new ArgumentOutOfRangeException(nameof(state));
            if (ActiveId == 0) return false;
            outcomes.Enqueue(new MoveOutcome(ActiveId, state, failure, cancellation, position));
            ActiveId = 0;
            State = state;
            LastFailure = failure;
            return true;
        }

        public bool TryDequeue(out MoveOutcome outcome)
        {
            if (outcomes.Count == 0) { outcome = default; return false; }
            outcome = outcomes.Dequeue();
            return true;
        }
    }
}
