using UnityEngine;

namespace OpenSourceTeam6.Movement.Unity
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class CharacterControllerMotor : MonoBehaviour
    {
        private CharacterController controller;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public Vector3 ActualVelocity { get; private set; }
        private void Awake() { controller = GetComponent<CharacterController>(); }

        public void Move(Vector3 velocity, float seconds)
        {
            ActualVelocity = Vector3.zero;
            if (seconds <= 0 || controller == null || !controller.enabled) return;
            Vector3 before = transform.position;
            CollisionFlags flags = controller.Move(velocity * seconds);
            ActualVelocity = (transform.position - before) / seconds;
            HitCeiling = (flags & CollisionFlags.Above) != 0;
        }

        public bool HitCeiling { get; private set; }
    }
}
