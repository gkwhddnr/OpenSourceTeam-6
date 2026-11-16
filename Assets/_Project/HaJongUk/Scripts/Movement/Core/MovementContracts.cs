using System;

namespace OpenSourceTeam6.Movement.Core
{
    public enum MoveState { Idle, Planning, Moving, Suspended, Arrived, Failed, Cancelled }
    public enum MoveFailure
    {
        None, InvalidDestination, NotReady, NoPath, PartialPath,
        PlanningTimeout, Stalled, DeadlineExceeded, TargetUnavailable
    }
    public enum CancelReason { None, Requested, Replaced, Disabled }

    public readonly struct MoveReceipt
    {
        public bool Accepted => RequestId > 0;
        public long RequestId { get; }
        public MoveFailure RejectReason { get; }
        public MoveReceipt(long requestId, MoveFailure rejectReason = MoveFailure.None)
        { RequestId = requestId; RejectReason = rejectReason; }
    }

    public readonly struct MoveOutcome
    {
        public long RequestId { get; }
        public MoveState Status { get; }
        public MoveFailure Failure { get; }
        public CancelReason Cancellation { get; }
        public WorldPoint FinalPosition { get; }
        public MoveOutcome(long id, MoveState status, MoveFailure failure, CancelReason cancellation, WorldPoint position)
        { RequestId = id; Status = status; Failure = failure; Cancellation = cancellation; FinalPosition = position; }
    }

    public readonly struct MovementSnapshot
    {
        public WorldPoint Position { get; }
        public WorldPoint ActualVelocity { get; }
        public MoveState State { get; }
        public long ActiveRequestId { get; }
        public MoveFailure LastFailure { get; }
        public MovementSnapshot(WorldPoint position, WorldPoint velocity, MoveState state, long id, MoveFailure failure)
        { Position = position; ActualVelocity = velocity; State = state; ActiveRequestId = id; LastFailure = failure; }
    }

    // All calls and event delivery belong to the Unity main thread.
    public interface IAgentMovement
    {
        MoveReceipt RequestMove(WorldPoint destination);
        bool Cancel(long requestId);
        MovementSnapshot Snapshot { get; }
        event Action<MoveOutcome> Finished;
    }

    public interface IPlayerInputGate
    {
        IDisposable AcquireLock(string reason);
        bool IsLocked { get; }
    }
}
