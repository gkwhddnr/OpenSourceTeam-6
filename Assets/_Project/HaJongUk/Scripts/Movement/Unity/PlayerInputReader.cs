using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.Movement.Unity
{
    public sealed class PlayerInputReader
    {
        private readonly InputActionAsset actions;
        public PlayerInputReader(InputActionAsset asset) { actions = asset; }
        private InputActionAsset ownedActions;
        private InputAction move;
        private InputAction look;
        public bool IsConfigured => actions != null;

        public void Initialize()
        {
            if (actions == null) throw new System.InvalidOperationException("Input actions are missing.");
            ownedActions = UnityEngine.Object.Instantiate(actions);
            move = ownedActions.FindAction("Gameplay/Move", true);
            look = ownedActions.FindAction("Gameplay/Look", true);
        }

        public void Enable() { if (ownedActions != null) ownedActions.Enable(); }
        public void Disable() { if (ownedActions != null) ownedActions.Disable(); }
        public void Dispose() { if (ownedActions != null) UnityEngine.Object.Destroy(ownedActions); }
        public Vector2 ReadMove() => move == null ? Vector2.zero : Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        public Vector2 ReadLook() => look == null ? Vector2.zero : look.ReadValue<Vector2>();
    }
}
