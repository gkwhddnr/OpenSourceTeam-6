using System;
using UnityEngine;
using UnityEngine.AI;

namespace OpenSourceTeam6.Customers.Unity
{
    internal enum NpcStepResult { Running, Success, Failure }

    internal sealed class NpcNavigation
    {
        private const float ArriveRadius = 0.35f;
        private const float TravelLimit = 45f;
        private const float SampleRadius = 0.5f;
        private const int ZoneSampleAttempts = 8;

        private readonly NavMeshPath path = new NavMeshPath();
        private Vector3? target;
        private Vector3 targetPosition;
        private float travelSeconds;
        private float travelDeadline = TravelLimit;

        public static NavMeshQueryFilter FilterFor(NavMeshAgent agent) =>
            new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        public static bool TrySampleZone(Vector3 center, Vector2 size, NavMeshQueryFilter filter,
            Predicate<Vector3> isOccupied, out Vector3 position, out bool hasWalkablePoint)
        {
            Vector2 halfSize = size * 0.5f;
            hasWalkablePoint = false;
            for (int i = 0; i < ZoneSampleAttempts; i++)
            {
                var candidate = new Vector3(
                    center.x + UnityEngine.Random.Range(-halfSize.x, halfSize.x),
                    center.y,
                    center.z + UnityEngine.Random.Range(-halfSize.y, halfSize.y));
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, SampleRadius, filter) ||
                    Mathf.Abs(hit.position.x - center.x) > halfSize.x ||
                    Mathf.Abs(hit.position.z - center.z) > halfSize.y)
                {
                    continue;
                }

                hasWalkablePoint = true;
                if (isOccupied != null && isOccupied(hit.position))
                    continue;

                position = hit.position;
                return true;
            }

            position = default;
            return false;
        }

        public void ClearTarget()
        {
            target = null;
            travelSeconds = 0f;
        }

        public bool TryFindApproach(NavMeshAgent agent, Vector3 interactionTarget, out Vector3 approach)
        {
            approach = default;
            if (!agent.isOnNavMesh) return false;

            Vector3 direction = agent.nextPosition - interactionTarget;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
            NavMeshQueryFilter filter = FilterFor(agent);
            for (int i = 0; i < 4; i++)
            {
                Vector3 candidate = interactionTarget + direction * 1.2f;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.75f, filter) &&
                    agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    approach = hit.position;
                    return true;
                }
                direction = new Vector3(-direction.z, 0f, direction.x);
            }
            return false;
        }

        public NpcStepResult MoveTo(NavMeshAgent agent, Transform destination, float elapsed) =>
            destination == null ? NpcStepResult.Failure : MoveTo(agent, destination.position, elapsed);

        public NpcStepResult MoveTo(NavMeshAgent agent, Vector3 destination, float elapsed)
        {
            if (!agent.isOnNavMesh)
                return NpcStepResult.Failure;

            if (!target.HasValue || target.Value != destination)
            {
                target = destination;
                travelSeconds = 0f;
                if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, SampleRadius, FilterFor(agent)))
                    return NpcStepResult.Failure;

                targetPosition = hit.position;
                if (!IsAtTarget(agent) &&
                    (!agent.CalculatePath(targetPosition, path) || path.status != NavMeshPathStatus.PathComplete ||
                     !agent.SetPath(path)))
                {
                    return NpcStepResult.Failure;
                }
                float distance = 0f;
                for (int i = 1; i < path.corners.Length; i++)
                    distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                // The test controls can reduce speed while this route is in progress.
                travelDeadline = Mathf.Max(TravelLimit, distance / 0.5f * 3f + 15f);
            }

            if (IsAtTarget(agent))
            {
                if (agent.hasPath)
                    agent.ResetPath();
                return NpcStepResult.Success;
            }

            travelSeconds += elapsed;
            return travelSeconds >= travelDeadline || agent.pathStatus == NavMeshPathStatus.PathInvalid
                ? NpcStepResult.Failure : NpcStepResult.Running;
        }

        private bool IsAtTarget(NavMeshAgent agent) =>
            (agent.nextPosition - targetPosition).sqrMagnitude <= ArriveRadius * ArriveRadius;
    }
}
