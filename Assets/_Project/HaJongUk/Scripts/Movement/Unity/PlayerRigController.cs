using OpenSourceTeam6.Camera.Core;
using OpenSourceTeam6.Camera.Unity;
using OpenSourceTeam6.Movement.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.Movement.Unity
{
    [DisallowMultipleComponent]
    public sealed class PlayerRigController : MonoBehaviour
    {
        [SerializeField] private MovementProfile profile;
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private Transform eyePivot;
        [SerializeField] private UnityEngine.Camera viewCamera;
        [SerializeField] private Renderer[] localVisuals = new Renderer[0];
        private PlayerInputReader input;
        private PlayerInputGate inputGate;
        private CharacterControllerMotor motor;
        private FirstPersonCameraPresenter cameraPresenter;
        private LookState look;
        private float verticalVelocity;
        public LookState Look => look;
        public IPlayerInputGate InputGate => inputGate;

        private void Awake()
        {
            var controller = GetComponent<CharacterController>();
            if (profile == null || !profile.IsValid || actions == null || controller == null ||
                eyePivot == null || viewCamera == null || !viewCamera.transform.IsChildOf(eyePivot))
            { Debug.LogError("PlayerRig has missing or invalid references/settings.", this); enabled = false; return; }
            input = new PlayerInputReader(actions);
            inputGate = new PlayerInputGate();
            motor = new CharacterControllerMotor(controller);
            cameraPresenter = new FirstPersonCameraPresenter(eyePivot, viewCamera, localVisuals);
            input.Initialize();
            cameraPresenter.Initialize();
            look = new LookState(transform.eulerAngles.y, 0f);
        }

        private void OnEnable() { input?.Enable(); }
        private void OnDisable() { input?.Disable(); inputGate?.Disable(); }
        private void OnDestroy() { input?.Dispose(); }
        private void LateUpdate() { cameraPresenter?.Apply(); }

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
