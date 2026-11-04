using OpenSourceTeam6.Movement.Core;
using UnityEngine;

namespace OpenSourceTeam6.Movement.Unity
{
    [CreateAssetMenu(menuName = "OpenSourceTeam6/Movement Profile")]
    public sealed class MovementProfile : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float walkSpeed = 3f;
        [SerializeField] private float gravity = -20f;
        [SerializeField, Range(0.01f, 2f)] private float lookSensitivity = 0.12f;
        [SerializeField, Min(0.01f)] private float projectionRadius = 0.5f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.3f;
        [SerializeField, Min(0.1f)] private float planningTimeout = 2f;
        [SerializeField, Min(0.1f)] private float stalledTimeout = 3f;
        [SerializeField, Min(0.1f)] private float requestTimeout = 30f;

        public float WalkSpeed => walkSpeed;
        public float Gravity => gravity;
        public float LookSensitivity => lookSensitivity;
        public float ProjectionRadius => projectionRadius;
        public float ArrivalDistance => arrivalDistance;
        public float PlanningTimeout => planningTimeout;
        public float StalledTimeout => stalledTimeout;
        public float RequestTimeout => requestTimeout;
        public bool IsValid => Positive(walkSpeed) && WorldPoint.Finite(gravity) && gravity < 0 &&
            Positive(lookSensitivity) && Positive(projectionRadius) && Positive(arrivalDistance) &&
            Positive(planningTimeout) && Positive(stalledTimeout) && Positive(requestTimeout);
        private static bool Positive(float value) => WorldPoint.Finite(value) && value > 0;
    }
}
