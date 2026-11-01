using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.Movement.Unity
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        private InputActionAsset ownedActions;
        private InputAction move;
        private InputAction look;
        public bool IsConfigured => actions != null;

        private void Awake()
        {
            if (actions == null) { Debug.LogError("Assign the player's InputActionAsset.", this); enabled = false; return; }
            ownedActions = Instantiate(actions);
            move = ownedActions.FindAction("Gameplay/Move", true);
            look = ownedActions.FindAction("Gameplay/Look", true);
        }

        private void OnEnable() { if (ownedActions != null) ownedActions.Enable(); }
        private void OnDisable() { if (ownedActions != null) ownedActions.Disable(); }
        private void OnDestroy() { if (ownedActions != null) Destroy(ownedActions); }
        public Vector2 ReadMove() => move == null ? Vector2.zero : Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        public Vector2 ReadLook() => look == null ? Vector2.zero : look.ReadValue<Vector2>();
    }
}
