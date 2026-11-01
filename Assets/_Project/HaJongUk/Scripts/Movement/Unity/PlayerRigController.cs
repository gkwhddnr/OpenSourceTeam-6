using OpenSourceTeam6.Camera.Core;
using OpenSourceTeam6.Camera.Unity;
using OpenSourceTeam6.Movement.Core;
using UnityEngine;

namespace OpenSourceTeam6.Movement.Unity
{
    [DisallowMultipleComponent]
    public sealed class PlayerRigController : MonoBehaviour
    {
        [SerializeField] private MovementProfile profile;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInputGate inputGate;
        [SerializeField] private CharacterControllerMotor motor;
        [SerializeField] private FirstPersonCameraPresenter cameraPresenter;
        private LookState look;
        private float verticalVelocity;
        public LookState Look => look;
        public IPlayerInputGate InputGate => inputGate;

        private void Awake()
        {
            if (profile == null || !profile.IsValid || input == null || !input.IsConfigured ||
                inputGate == null || motor == null || cameraPresenter == null || !cameraPresenter.IsConfigured)
            { Debug.LogError("PlayerRig has missing or invalid references/settings.", this); enabled = false; return; }
            look = new LookState(transform.eulerAngles.y, 0f);
        }

        private void Update()
        {
            bool skipLook = inputGate.PrepareFrame();
            Vector2 movement = inputGate.IsLocked ? Vector2.zero : input.ReadMove();
            Vector2 delta = skipLook ? Vector2.zero : input.ReadLook();
            look = LookSolver.Step(look, delta.x, delta.y, profile.LookSensitivity);
            transform.rotation = Quaternion.Euler(0f, look.YawDegrees, 0f);
            float dt = MovementSolver.StepSeconds(Time.deltaTime);
            WorldPoint horizontal = MovementSolver.HorizontalVelocity(movement.x, movement.y, look.YawDegrees, profile.WalkSpeed);
            if (motor.HitCeiling && verticalVelocity > 0) verticalVelocity = 0;
            verticalVelocity = MovementSolver.VerticalVelocity(verticalVelocity, motor.IsGrounded, profile.Gravity, dt);
            motor.Move(new Vector3(horizontal.X, verticalVelocity, horizontal.Z), dt);
            cameraPresenter.Present(look);
        }
    }
}
