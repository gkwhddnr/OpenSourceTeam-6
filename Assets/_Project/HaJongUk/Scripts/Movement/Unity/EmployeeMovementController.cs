using System;
using OpenSourceTeam6.Movement.Core;
using UnityEngine;
using UnityEngine.AI;

namespace OpenSourceTeam6.Movement.Unity
{
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class EmployeeMovementController : MonoBehaviour, IAgentMovement
    {
        [SerializeField] private MovementProfile profile;
        private NavMeshAgentDriver driver;
        private readonly MoveRequestTracker tracker = new MoveRequestTracker();
        private float elapsed;
        private float planningElapsed;
        private float stalledElapsed;
        private Vector3 progressPosition;
        private Vector3 destination;
        private int requestedFrame;
        private bool closing;
        private bool dispatching;
        private bool pathResolved;
        private int mainThread;
        public event Action<MoveOutcome> Finished;
        public MovementSnapshot Snapshot
        {
            get
            {
                RequireMainThread();
                return new MovementSnapshot(Point(transform.position), Point(driver == null ? Vector3.zero : driver.Velocity),
                    tracker.State, tracker.ActiveId, tracker.LastFailure);
            }
        }

        private void Awake()
        {
            mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            driver = new NavMeshAgentDriver(GetComponent<NavMeshAgent>());
            if (profile == null || !profile.IsValid || GetComponent<NavMeshAgent>() == null)
            { Debug.LogError("EmployeeMovement requires a valid profile and driver.", this); enabled = false; return; }
            driver.Configure(profile);
        }

        private void OnEnable() { closing = false; }

        public MoveReceipt RequestMove(WorldPoint target)
        {
            RequireMainThread();
            if (!target.IsFinite) return new MoveReceipt(0, MoveFailure.InvalidDestination);
            if (closing || !isActiveAndEnabled || profile == null || !profile.IsValid ||
                driver == null || !driver.IsReady || !tracker.CanBegin)
                return new MoveReceipt(0, MoveFailure.NotReady);
            if (!driver.TryProject(new Vector3(target.X, target.Y, target.Z), profile.ProjectionRadius, out Vector3 projected))
                return new MoveReceipt(0, MoveFailure.InvalidDestination);

            MoveReceipt receipt = tracker.Begin(Point(transform.position));
            destination = projected;
            elapsed = planningElapsed = stalledElapsed = 0f;
            progressPosition = transform.position;
            pathResolved = false;
            requestedFrame = Time.frameCount;
            if (!driver.Begin(destination)) Finish(MoveState.Failed, MoveFailure.NoPath);
            return receipt;
        }

        public bool Cancel(long requestId)
        {
            RequireMainThread();
            if (!tracker.Cancel(requestId, Point(transform.position))) return false;
            driver.Stop();
            return true;
        }

        private void Update()
        {
            if (tracker.ActiveId == 0) return;
            if (!driver.IsReady) { Finish(MoveState.Failed, MoveFailure.TargetUnavailable); return; }
            if (Time.deltaTime <= 0f)
            { driver.Pause(true); tracker.SetPhase(MoveState.Suspended); return; }
            driver.Pause(false);
            elapsed += Time.deltaTime;
            if (elapsed >= profile.RequestTimeout) { Finish(MoveState.Failed, MoveFailure.DeadlineExceeded); return; }
            // Never read the previous path's arrival state in the request frame.
            if (Time.frameCount <= requestedFrame) return;
            if (!pathResolved || driver.PathPending)
            {
                tracker.SetPhase(MoveState.Planning);
                planningElapsed += Time.deltaTime;
                if (planningElapsed >= profile.PlanningTimeout)
                { Finish(MoveState.Failed, MoveFailure.PlanningTimeout); return; }
                if (driver.PathPending) return;
                // A zero-length request may have no stored path after resolution.
                if (Vector3.Distance(transform.position, destination) <= profile.ArrivalDistance &&
                    driver.Velocity.sqrMagnitude <= 0.0025f)
                { Finish(MoveState.Arrived); return; }
                if (!ValidatePath()) return;
                pathResolved = true;
            }
            tracker.SetPhase(MoveState.Moving);
            float distance = driver.RemainingDistance;
            // Unity may release hasPath once the completed path reaches its end.
            // Require the previously resolved complete path and physical proximity as well.
            bool atDestination = Vector3.Distance(transform.position, destination) <= profile.ArrivalDistance + 0.05f;
            bool settledOnPath = WorldPoint.Finite(distance) && distance <= profile.ArrivalDistance + 0.01f &&
                driver.Velocity.sqrMagnitude <= 0.0025f;
            if (driver.PathStatus == NavMeshPathStatus.PathComplete && atDestination && (!driver.HasPath || settledOnPath))
            { Finish(MoveState.Arrived); return; }
            if (!ValidatePath()) return;
            if ((transform.position - progressPosition).sqrMagnitude >= 0.0025f)
            { progressPosition = transform.position; stalledElapsed = 0f; }
            else stalledElapsed += Time.deltaTime;
            if (stalledElapsed >= profile.StalledTimeout) Finish(MoveState.Failed, MoveFailure.Stalled);
        }

        private bool ValidatePath()
        {
            if (driver.PathStatus == NavMeshPathStatus.PathPartial)
            { Finish(MoveState.Failed, MoveFailure.PartialPath); return false; }
            if (!driver.HasPath || driver.PathStatus == NavMeshPathStatus.PathInvalid)
            { Finish(MoveState.Failed, MoveFailure.NoPath); return false; }
            return true;
        }

        private void Finish(MoveState state, MoveFailure failure = MoveFailure.None)
        {
            tracker.Complete(state, Point(transform.position), failure);
            driver.Stop();
        }

        private void LateUpdate() { Dispatch(); }
        private void Dispatch()
        {
            if (dispatching) return;
            dispatching = true;
            try
            {
                int count = tracker.PendingCount;
                Delegate[] subscribers = Finished?.GetInvocationList();
                for (int i = 0; i < count && tracker.TryDequeue(out MoveOutcome result); ++i)
                {
                    if (subscribers == null) continue;
                    foreach (Action<MoveOutcome> handler in subscribers)
                    {
                        try { handler(result); }
                        catch (Exception exception) { Debug.LogException(exception, this); }
                    }
                }
            }
            finally { dispatching = false; }
            // Disable can be requested by a subscriber while this dispatch is in progress.
            if (closing && tracker.PendingCount != 0) Dispatch();
        }

        private void OnDisable()
        {
            closing = true;
            tracker.Cancel(tracker.ActiveId, Point(transform.position), CancelReason.Disabled);
            if (driver != null) driver.Stop();
            Dispatch();
        }

        private void RequireMainThread()
        {
            if (mainThread != 0 && mainThread != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Movement API must be called on the Unity main thread.");
        }
        private static WorldPoint Point(Vector3 point) => new WorldPoint(point.x, point.y, point.z);
    }
}
