using UnityEngine;
namespace OpenSourceTeam6.Time.Unity {
 public sealed class TimeDemoPanel : MonoBehaviour {
 public BusinessTimeController timeSource;
 public Camera viewCamera;
 [Min(1)] public float fullDayDurationSeconds = 60;
 private float elapsed;
 private bool paused;
 private void Update() {
 if (!timeSource || paused) return;
 elapsed += UnityEngine.Time.deltaTime;
 if (timeSource.Clock.Phase == Core.BusinessPhase.Ready) timeSource.BeginDay(1);
 timeSource.ApplyExternalProgress(timeSource.Clock.DayId, Mathf.Min(elapsed / Mathf.Max(fullDayDurationSeconds,1), 1));
 }
 private void OnGUI() {
 GUILayout.BeginArea(new Rect(12,12,310,210), GUI.skin.box);
 GUILayout.Label("TIME TEST | September | 00:00 - 24:00");
 GUILayout.Label("Hour: " + timeSource.GameHour.ToString("F2") + " | " + timeSource.Clock.Phase);
 if(GUILayout.Button(paused ? "Resume" : "Pause")) paused=!paused;
 if(GUILayout.Button("Next day / restart")) {
 timeSource.ApplyExternalProgress(timeSource.Clock.DayId,1);
 timeSource.Clock.ConfirmSettlement(timeSource.Clock.DayId);
 timeSource.BeginDay(timeSource.Clock.DayId+1); elapsed=0;
 }
 GUILayout.BeginHorizontal();
 if(GUILayout.Button("East")) viewCamera.transform.rotation=Quaternion.Euler(-15,90,0);
 if(GUILayout.Button("West")) viewCamera.transform.rotation=Quaternion.Euler(-15,270,0);
 if(GUILayout.Button("Up")) viewCamera.transform.rotation=Quaternion.Euler(-70,90,0);
 GUILayout.EndHorizontal();
 GUILayout.Label("24h cycle: " + Mathf.Max(fullDayDurationSeconds,1).ToString("F0") + " seconds. No scene loading buttons.");
 GUILayout.EndArea();
 }
 }
}
