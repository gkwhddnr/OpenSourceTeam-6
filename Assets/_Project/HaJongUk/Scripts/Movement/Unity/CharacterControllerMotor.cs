using UnityEngine;

namespace OpenSourceTeam6.Movement.Unity
{
    public sealed class CharacterControllerMotor
    {
        private CharacterController controller;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public Vector3 ActualVelocity { get; private set; }
        public CharacterControllerMotor(CharacterController controller) { this.controller = controller; }

        public void Move(Vector3 velocity, float seconds)
        {
            ActualVelocity = Vector3.zero;
            if (seconds <= 0 || controller == null || !controller.enabled) return;
            Vector3 before = controller.transform.position;
            CollisionFlags flags = controller.Move(velocity * seconds);
            ActualVelocity = (controller.transform.position - before) / seconds;
            HitCeiling = (flags & CollisionFlags.Above) != 0;
        }

        public bool HitCeiling { get; private set; }
    }
}
