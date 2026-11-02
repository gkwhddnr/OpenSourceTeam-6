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
        public void Initialize()
        {
            if (actions == null) throw new System.InvalidOperationException("Input actions are missing.");
            if (ownedActions != null) throw new System.InvalidOperationException("Input actions are already initialized.");
            var instance = UnityEngine.Object.Instantiate(actions);
            try
            {
                var moveAction = instance.FindAction("Gameplay/Move", true);
                var lookAction = instance.FindAction("Gameplay/Look", true);
                ownedActions = instance;
                move = moveAction;
                look = lookAction;
            }
            catch
            {
                UnityEngine.Object.Destroy(instance);
                throw;
            }
        }

        public void Enable() { if (ownedActions != null) ownedActions.Enable(); }
        public void Disable() { if (ownedActions != null) ownedActions.Disable(); }
        public void Dispose()
        {
            if (ownedActions == null) return;
            ownedActions.Disable();
            UnityEngine.Object.Destroy(ownedActions);
            ownedActions = null;
            move = null;
            look = null;
        }
        public Vector2 ReadMove() => move == null ? Vector2.zero : Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        public Vector2 ReadLook() => look == null ? Vector2.zero : look.ReadValue<Vector2>();
    }
}
