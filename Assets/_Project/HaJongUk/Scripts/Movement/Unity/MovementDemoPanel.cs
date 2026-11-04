using OpenSourceTeam6.Movement.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.Movement.Unity
{
    // Small integration harness; no employee decision/story logic lives here.
    public sealed class MovementDemoPanel : MonoBehaviour
    {
        [SerializeField] private EmployeeMovementController employee;
        [SerializeField] private Transform[] destinations;
        private string lastResult = "Ready";
        private void OnEnable() { if (employee != null) employee.Finished += OnFinished; }
        private void OnDisable() { if (employee != null) employee.Finished -= OnFinished; }
        private void OnFinished(MoveOutcome outcome)
        { lastResult = $"#{outcome.RequestId}: {outcome.Status} / {outcome.Failure} / {outcome.Cancellation}"; }

        private void Update()
        {
            if (employee == null || Keyboard.current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) Request(0);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) Request(1);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) Request(2);
            if (keyboard.cKey.wasPressedThisFrame) employee.Cancel(employee.Snapshot.ActiveRequestId);
        }

        private void Request(int index)
        {
            if (destinations == null || index >= destinations.Length || destinations[index] == null)
            {
                lastResult = $"Target {index + 1} is not assigned";
                return;
            }
            Vector3 p = destinations[index].position;
            MoveReceipt receipt = employee.RequestMove(new WorldPoint(p.x, p.y, p.z));
            lastResult = receipt.Accepted ? $"Request #{receipt.RequestId}" : $"Rejected: {receipt.RejectReason}";
        }

        private void OnGUI()
        {
            if (employee == null) return;
            GUILayout.BeginArea(new Rect(18, 18, 440, 155), GUI.skin.box);
            GUILayout.Label("CAMERA / MOVEMENT - INTEGRATION SANDBOX");
            GUILayout.Label("WASD: Walk   Mouse: Look   Esc: Release cursor");
            GUILayout.Label("Click: Capture cursor   1 / 2: Send employee");
            GUILayout.Label("3: Target 3   C: Cancel (number row or numpad)");
            GUILayout.Label($"Employee: {employee.Snapshot.State}   {lastResult}");
            GUILayout.EndArea();
        }
    }
}
