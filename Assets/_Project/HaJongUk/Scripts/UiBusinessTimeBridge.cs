using OpenSourceTeam6.Time.Core;
using OpenSourceTeam6.Time.Unity;
using OpenSourceTeam6.UI.Core;
using OpenSourceTeam6.UI.Unity;
using UnityEngine;

/// <summary>Mirrors the UI-owned countdown into the clock driving the environment.</summary>
[DefaultExecutionOrder(50), DisallowMultipleComponent]
public sealed class UiBusinessTimeBridge : MonoBehaviour
{
    [KoreanInspectorLabel("영업 UI"), SerializeField] private StoreUiSessionController ui;
    [KoreanInspectorLabel("시간 시스템"), SerializeField] private BusinessTimeController timeSystem;
    private bool ownsClock;
    private int activeDay;

    public bool Configure(StoreUiSessionController uiController, BusinessTimeController clock)
    {
        if (ownsClock) return false;
        ui = uiController;
        timeSystem = clock;
        return Initialize();
    }

    private void OnEnable()
    {
        if (ui && timeSystem) Initialize();
    }

    private bool Initialize()
    {
        if (ownsClock) return true;
        if (!ui || !timeSystem || !timeSystem.AcquireExternalClock())
        {
            Debug.LogError("UI 시간 연동을 시작할 수 없습니다. 참조와 시계 소유 상태를 확인하세요.", this);
            return false;
        }
        ownsClock = true;
        ui.DayStarted += BeginDay;
        if (ui.CurrentMode == UiScreenMode.InGame)
        {
            if (timeSystem.Clock.Phase == BusinessPhase.Open) activeDay = timeSystem.Clock.DayId;
            else BeginDay();
        }
        return true;
    }

    private void BeginDay()
    {
        if (!ownsClock || !timeSystem || !ui || ui.TotalDaySeconds <= 0 ||
            float.IsNaN(ui.TotalDaySeconds) || float.IsInfinity(ui.TotalDaySeconds)) return;
        if (timeSystem.Clock.Phase == BusinessPhase.Closing)
            timeSystem.Clock.ConfirmSettlement(timeSystem.Clock.DayId);
        if (timeSystem.Clock.Phase != BusinessPhase.Ready && timeSystem.Clock.Phase != BusinessPhase.Settled)
        {
            Debug.LogError("이전 일차가 끝나지 않아 새 UI 일차를 시작할 수 없습니다.", this);
            return;
        }
        int next = timeSystem.Clock.DayId + 1;
        if (timeSystem.BeginDay(next)) activeDay = next;
    }

    private void Update()
    {
        if (!ownsClock || !ui || !timeSystem || activeDay == 0 || timeSystem.Clock.DayId != activeDay) return;
        if (ui.CurrentMode == UiScreenMode.InGame && ui.TotalDaySeconds > 0)
        {
            float progress = Mathf.Clamp01(1f - ui.RemainingDaySeconds / ui.TotalDaySeconds);
            timeSystem.ApplyExternalProgress(activeDay, progress);
        }
        else if (ui.CurrentMode == UiScreenMode.Settlement && timeSystem.Clock.Phase == BusinessPhase.Open)
        {
            timeSystem.ApplyExternalProgress(activeDay, 1);
            timeSystem.Clock.ConfirmSettlement(activeDay);
        }
        else if (ui.CurrentMode == UiScreenMode.Lobby)
        {
            timeSystem.Clock.CancelDay(activeDay);
            activeDay = 0;
        }
    }

    private void OnDisable()
    {
        if (ui) ui.DayStarted -= BeginDay;
        if (ownsClock && timeSystem) timeSystem.ReleaseExternalClock();
        ownsClock = false;
        activeDay = 0;
    }
}
