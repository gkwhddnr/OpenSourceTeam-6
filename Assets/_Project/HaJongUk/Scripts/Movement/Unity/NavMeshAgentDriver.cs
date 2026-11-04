using UnityEngine;
using UnityEngine.AI;

namespace OpenSourceTeam6.Movement.Unity
{
    public sealed class NavMeshAgentDriver
    {
        private NavMeshAgent agent;
        public NavMeshAgentDriver(NavMeshAgent agent) { this.agent = agent; }
        private NavMeshAgent Agent => agent;
        public bool IsReady => Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh;
        public bool PathPending => Agent.pathPending;
        public bool HasPath => Agent.hasPath;
        public NavMeshPathStatus PathStatus => Agent.pathStatus;
        public float RemainingDistance => Agent.remainingDistance;
        public Vector3 Velocity => IsReady ? Agent.velocity : Vector3.zero;

        public void Configure(MovementProfile profile)
        {
            Agent.speed = profile.WalkSpeed;
            Agent.stoppingDistance = profile.ArrivalDistance;
            Agent.autoBraking = true;
            Agent.autoRepath = false;
        }

        public bool TryProject(Vector3 point, float radius, out Vector3 projected)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask };
            bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, radius, filter);
            projected = hit.position;
            return found && Mathf.Abs(hit.position.y - point.y) <= radius;
        }

        public bool Begin(Vector3 destination)
        {
            if (!IsReady) return false;
            Agent.ResetPath();
            Agent.isStopped = false;
            return Agent.SetDestination(destination);
        }

        public void Pause(bool paused) { if (IsReady) Agent.isStopped = paused; }
        public void Stop()
        {
            if (!IsReady) return;
            Agent.isStopped = true;
            Agent.ResetPath();
            Agent.velocity = Vector3.zero;
        }
    }
}
