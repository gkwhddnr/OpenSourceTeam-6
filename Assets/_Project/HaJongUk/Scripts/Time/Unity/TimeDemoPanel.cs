using UnityEngine;
using OpenSourceTeam6.Time.Core;

namespace OpenSourceTeam6.Time.Unity
{
    [RequireComponent(typeof(BusinessTimeController), typeof(TimeEnvironmentController))]
    public sealed class TimeDemoPanel : MonoBehaviour
    {
        [KoreanInspectorLabel("테스트 하루 길이 (초)"), Min(1)] public float fullDayDurationSeconds = 60;
        [KoreanInspectorLabel("시점 변경 버튼 표시"), SerializeField] private bool showViewControls = true;

        private BusinessTimeController timeSource;
        private TimeEnvironmentController environment;
        private float nextLabelRefresh;
        private Vector2 panelScroll;
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
            timeSource.ConfigureDemoClock(fullDayDurationSeconds);
            nextLabelRefresh = 0;
            RefreshLabels();
        }

        private void Update()
        {
            if (!timeSource || !environment) return;
            timeSource.ConfigureDemoClock(fullDayDurationSeconds);
            if (UnityEngine.Time.unscaledTime >= nextLabelRefresh) RefreshLabels();
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

        public Rect PanelRect => new Rect(Screen.width - 12 - Mathf.Min(360, Mathf.Max(1, (Screen.width - 48) * .5f)),
            12, Mathf.Min(360, Mathf.Max(1, (Screen.width - 48) * .5f)), Mathf.Max(1, Mathf.Min(350, Screen.height - 24)));

        private void OnGUI()
        {
            if (!timeSource || !environment) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label("TIME TEST | September | 08:00 - next day 08:00");
            if (!showViewControls) GUILayout.Label("Esc: release cursor | Click world: resume");
            GUILayout.Label(hourLabel);
            if (GUILayout.Button(timeSource.Clock.IsPaused ? "Resume" : "Pause"))
                timeSource.Clock.IsPaused = !timeSource.Clock.IsPaused;
            if (GUILayout.Button("Next day / restart")) RestartDay();

            if (showViewControls)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("East")) environment.SetViewRotation(Quaternion.Euler(-15, 90, 0));
                if (GUILayout.Button("West")) environment.SetViewRotation(Quaternion.Euler(-15, 270, 0));
                if (GUILayout.Button("Up")) environment.SetViewRotation(Quaternion.Euler(-70, 90, 0));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(cycleLabel);
            environment.IsRain = GUILayout.Toggle(environment.IsRain, "isRain | Rain weather");
            GUILayout.Label("Rain fog strength: " + Mathf.RoundToInt(environment.RainFogPercent) + "%");
            environment.RainFogPercent = GUILayout.HorizontalSlider(environment.RainFogPercent, 0, 100);
            GUILayout.Label(rainLabel);
            GUILayout.Label(cloudLabel);
            GUILayout.Label(splashLabel);
            GUILayout.Label("Clouds cover the sky before rain begins.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void RestartDay()
        {
            timeSource.RestartDemoDay();
            RefreshLabels();
        }
    }
}
