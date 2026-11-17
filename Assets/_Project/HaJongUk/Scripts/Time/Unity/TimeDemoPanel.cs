using UnityEngine;
using OpenSourceTeam6.Time.Core;

namespace OpenSourceTeam6.Time.Unity
{
    [RequireComponent(typeof(BusinessTimeController), typeof(TimeEnvironmentController))]
    public sealed class TimeDemoPanel : MonoBehaviour
    {
        [Min(1)] public float fullDayDurationSeconds = 60;

        private BusinessTimeController timeSource;
        private TimeEnvironmentController environment;
        private float elapsed, nextLabelRefresh;
        private string hourLabel = "", cycleLabel = "", rainLabel = "", cloudLabel = "", splashLabel = "";

        private void OnEnable()
        {
            timeSource = GetComponent<BusinessTimeController>();
            environment = GetComponent<TimeEnvironmentController>();
            if (!timeSource || !environment)
            {
                Debug.LogError("Time demo requires a clock and environment on the same object.", this);
                enabled = false;
                return;
            }
            nextLabelRefresh = 0;
            RefreshLabels();
        }

        private void Update()
        {
            if (!timeSource || !environment) return;
            if (UnityEngine.Time.unscaledTime >= nextLabelRefresh) RefreshLabels();
            if (timeSource.Clock.IsPaused) return;

            elapsed += UnityEngine.Time.deltaTime;
            if (timeSource.Clock.Phase == BusinessPhase.Ready) timeSource.BeginDay(1);
            timeSource.ApplyExternalProgress(timeSource.Clock.DayId,
                Mathf.Min(elapsed / Mathf.Max(fullDayDurationSeconds, 1), 1));
        }

        private void RefreshLabels()
        {
            nextLabelRefresh = UnityEngine.Time.unscaledTime + .1f;
            hourLabel = "Hour: " + timeSource.GameHour.ToString("F2") + " | " + timeSource.Clock.Phase;
            cycleLabel = "24h cycle: " + Mathf.Max(fullDayDurationSeconds, 1).ToString("F0") + " seconds.";
            rainLabel = "Rain intensity: " + (environment.RainIntensity * 100).ToString("F0")
                + "% | Drops: " + environment.VisibleDropCount;
            cloudLabel = "Rain clouds: " + (environment.CloudIntensity * 100).ToString("F0") + "%";
            splashLabel = "Impact splashes: " + environment.VisibleSplashCount;
        }

        private void OnGUI()
        {
            if (!timeSource || !environment) return;
            GUILayout.BeginArea(new Rect(12, 12, 360, 305), GUI.skin.box);
            GUILayout.Label("TIME TEST | September | 08:00 - next day 08:00");
            GUILayout.Label(hourLabel);
            if (GUILayout.Button(timeSource.Clock.IsPaused ? "Resume" : "Pause"))
                timeSource.Clock.IsPaused = !timeSource.Clock.IsPaused;
            if (GUILayout.Button("Next day / restart")) RestartDay();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("East")) environment.SetViewRotation(Quaternion.Euler(-15, 90, 0));
            if (GUILayout.Button("West")) environment.SetViewRotation(Quaternion.Euler(-15, 270, 0));
            if (GUILayout.Button("Up")) environment.SetViewRotation(Quaternion.Euler(-70, 90, 0));
            GUILayout.EndHorizontal();
            GUILayout.Label(cycleLabel);
            environment.IsRain = GUILayout.Toggle(environment.IsRain, "isRain | Rain weather");
            GUILayout.Label(rainLabel);
            GUILayout.Label(cloudLabel);
            GUILayout.Label(splashLabel);
            GUILayout.Label("Clouds cover the sky before rain begins.");
            GUILayout.EndArea();
        }

        private void RestartDay()
        {
            timeSource.Clock.IsPaused = false;
            timeSource.ApplyExternalProgress(timeSource.Clock.DayId, 1);
            timeSource.Clock.ConfirmSettlement(timeSource.Clock.DayId);
            if (timeSource.BeginDay(timeSource.Clock.DayId + 1)) elapsed = 0;
            RefreshLabels();
        }
    }
}
