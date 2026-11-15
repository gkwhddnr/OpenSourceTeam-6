using OpenSourceTeam6.Movement.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenSourceTeam6.Movement.Unity
{
    // Small integration harness; no employee decision/story logic lives here.
    [DefaultExecutionOrder(-100)]
    public sealed class MovementDemoPanel : MonoBehaviour
    {
        [SerializeField] private EmployeeMovementController employee;
        [SerializeField] private Transform[] destinations;
        private static readonly Key[] NumberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
        private static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9, Key.Numpad0 };
        private int selectedIndex = -1;
        private Transform selectedTarget;
        private Vector3 requestedPosition;
        private long requestId;
        private float nextTargetCheck;
        private Vector2 destinationScroll;
        private PlayerRigController player;
        private System.IDisposable panelInputLock;
        private string lastResult = "Ready";
        private void Start() { player = FindFirstObjectByType<PlayerRigController>(); }
        private void OnEnable() { if (employee != null) employee.Finished += OnFinished; }
        private void OnDisable()
        {
            if (employee != null) employee.Finished -= OnFinished;
            selectedIndex = -1;
            panelInputLock?.Dispose();
            panelInputLock = null;
        }
        private void OnFinished(MoveOutcome outcome)
        {
            if (outcome.RequestId != requestId) return;
            lastResult = $"#{outcome.RequestId}: {outcome.Status} / {outcome.Failure} / {outcome.Cancellation}";
            if (outcome.Status != MoveState.Arrived) selectedIndex = -1;
        }

        private void Update()
        {
            UpdatePanelInputLock();
            if (employee == null) return;
            UpdateSelectedTarget();
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            for (int i = 0; i < NumberKeys.Length; i++)
                if (keyboard[NumberKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame) Request(i);
            if (keyboard.cKey.wasPressedThisFrame) Cancel();
        }

        private Rect PanelRect => new Rect(18, 18, 440, Mathf.Max(155, Mathf.Min(420, Screen.height - 36)));

        private void UpdatePanelInputLock()
        {
            bool hovering = false;
            if (employee != null && Cursor.visible && Mouse.current != null)
            {
                Vector2 position = Mouse.current.position.ReadValue();
                hovering = PanelRect.Contains(new Vector2(position.x, Screen.height - position.y));
            }
            // Run before the player rig so clicking a target button does not recapture the cursor.
            if (hovering && panelInputLock == null && player != null && player.InputGate != null)
                panelInputLock = player.InputGate.AcquireLock("Movement demo controls");
            else if (!hovering && panelInputLock != null)
            {
                panelInputLock.Dispose();
                panelInputLock = null;
            }
        }

        private void Cancel()
        {
            selectedIndex = -1;
            employee.Cancel(employee.Snapshot.ActiveRequestId);
        }

        private void UpdateSelectedTarget()
        {
            if (selectedIndex < 0 || Time.unscaledTime < nextTargetCheck) return;
            nextTargetCheck = Time.unscaledTime + 0.2f;
            // Stop following when another system takes ownership of movement.
            long activeId = employee.Snapshot.ActiveRequestId;
            if (activeId != 0 && activeId != requestId) { selectedIndex = -1; return; }
            if (destinations == null || selectedIndex >= destinations.Length || destinations[selectedIndex] == null)
            {
                if (activeId == requestId) employee.Cancel(requestId);
                selectedIndex = -1;
                lastResult = "Selected target is no longer assigned";
                return;
            }
            Transform target = destinations[selectedIndex];
            if (target != selectedTarget || (DestinationPosition(target) - requestedPosition).sqrMagnitude >= 0.0001f)
                Request(selectedIndex);
        }

        private static Vector3 DestinationPosition(Transform target)
        {
            // The sandbox marker can be moved independently of its empty target root.
            Transform marker = target.Find("MarkerVisual");
            return marker != null ? marker.position : target.position;
        }

        private void Request(int index)
        {
            if (employee == null) return;
            if (destinations == null || index < 0 || index >= destinations.Length || destinations[index] == null)
            {
                lastResult = $"Target {index + 1} is not assigned";
                return;
            }
            Vector3 p = DestinationPosition(destinations[index]);
            MoveReceipt receipt = employee.RequestMove(new WorldPoint(p.x, p.y, p.z));
            if (receipt.Accepted)
            {
                selectedIndex = index;
                selectedTarget = destinations[index];
                requestedPosition = p;
                requestId = receipt.RequestId;
            }
            else if (selectedIndex == index)
            {
                // Do not retry an invalid moved target every frame; allow manual retry.
                selectedIndex = -1;
            }
            lastResult = receipt.Accepted ? $"Request #{receipt.RequestId}" : $"Rejected: {receipt.RejectReason}";
        }

        private void OnGUI()
        {
            if (employee == null) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("CAMERA / MOVEMENT - INTEGRATION SANDBOX");
            GUILayout.Label("WASD: Walk   Mouse: Look   Esc: Release cursor");
            GUILayout.Label("1-9 / 0: Targets 1-10 (number row or numpad)");
            GUILayout.Label("Esc: Use target buttons   C: Cancel");
            GUILayout.Label($"Employee: {employee.Snapshot.State}   {lastResult}");
            destinationScroll = GUILayout.BeginScrollView(destinationScroll);
            if (destinations != null)
                for (int i = 0; i < destinations.Length; i++)
                {
                    Transform target = destinations[i];
                    GUI.enabled = target != null;
                    string label = target == null ? $"{i + 1}: (unassigned)" : $"{i + 1}: {target.name}  {DestinationPosition(target):F1}";
                    if (GUILayout.Button(label)) Request(i);
                }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
