using OpenSourceTeam6.Movement.Unity;
using OpenSourceTeam6.Time.Unity;
using UnityEngine;

/// <summary>Adapts the standalone time test panel to the movement rig's cursor gate.</summary>
[DefaultExecutionOrder(-90)]
[RequireComponent(typeof(TimeDemoPanel))]
public sealed class TimeMovementInputBridge : MonoBehaviour
{
    [KoreanInspectorLabel("플레이어"), SerializeField] private PlayerRigController player;
    private TimeDemoPanel panel;
    private readonly PanelInputLock inputLock = new PanelInputLock("Time demo controls");

    private void OnEnable() => panel = GetComponent<TimeDemoPanel>();

    private void Update()
    {
        var gate = player && player.isActiveAndEnabled ? player.InputGate : null;
        inputLock.Update(gate, panel && panel.isActiveAndEnabled && PanelInputLock.PointerIsOver(panel.PanelRect));
    }

    private void OnDisable() => inputLock.Dispose();
}
