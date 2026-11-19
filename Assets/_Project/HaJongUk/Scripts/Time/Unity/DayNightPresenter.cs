using UnityEngine;
using UnityEngine.Rendering;
using OpenSourceTeam6.Time.Core;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Owns solar art settings and sun/ambient output, not the clock.</summary>
    [DisallowMultipleComponent]
    public sealed class DayNightPresenter : MonoBehaviour
    {
        [KoreanInspectorLabel("기준 월"), Tooltip("8~11월의 연출용 시간입니다. 실제 천문 관측값은 아닙니다.")]
        [SerializeField, Range(8, 11)] private int month = 9;
        [KoreanInspectorLabel("8월 일출·일몰"), Tooltip("X: 일출 시각, Y: 일몰 시각"), SerializeField] private Vector2 august = new Vector2(5.5f, 19.5f);
        [KoreanInspectorLabel("9월 일출·일몰"), Tooltip("X: 일출 시각, Y: 일몰 시각"), SerializeField] private Vector2 september = new Vector2(6, 18.5f);
        [KoreanInspectorLabel("10월 일출·일몰"), Tooltip("X: 일출 시각, Y: 일몰 시각"), SerializeField] private Vector2 october = new Vector2(6.5f, 18);
        [KoreanInspectorLabel("11월 일출·일몰"), Tooltip("X: 일출 시각, Y: 일몰 시각"), SerializeField] private Vector2 november = new Vector2(7, 17.5f);
        [KoreanInspectorLabel("낮 햇빛 강도"), SerializeField, Min(0)] private float daylightIntensity = 1;
        [KoreanInspectorLabel("밤 환경광 색상"), SerializeField] private Color nightAmbient = new Color(.08f, .1f, .16f);
        [KoreanInspectorLabel("낮 환경광 색상"), SerializeField] private Color dayAmbient = new Color(.55f, .6f, .7f);
        public Vector3 SunDirection { get; private set; } = Vector3.up;
        public float NightBlend { get; private set; }
        private Light ownedSun, originalSun;
        private Quaternion originalRotation;
        private float originalIntensity;
        private Color originalColor, originalAmbient;
        private AmbientMode originalMode;
        private bool captured;

        public SunSample Evaluate(float gameHour)
        {
            Vector2 range = month == 8 ? august : month == 9 ? september : month == 10 ? october : november;
            return CelestialMath.SampleSun(gameHour, range.x, range.y);
        }

        public void Render(Light sun, SunSample sample)
        {
            if (!isActiveAndEnabled || !sun) return;
            if (!captured || ownedSun != sun)
            {
                Release();
                ownedSun = sun;
                originalSun = RenderSettings.sun;
                originalRotation = sun.transform.rotation;
                originalIntensity = sun.intensity;
                originalColor = sun.color;
                originalAmbient = RenderSettings.ambientLight;
                originalMode = RenderSettings.ambientMode;
                captured = true;
            }
            SunDirection = new Vector3(sample.Direction.X, sample.Direction.Y, sample.Direction.Z);
            NightBlend = sample.NightBlend;
            RenderSettings.sun = sun;
            sun.transform.rotation = Quaternion.LookRotation(-SunDirection, Vector3.up);
            sun.intensity = Mathf.Max(0, daylightIntensity) * sample.Elevation;
            sun.color = Color.Lerp(new Color(1, .55f, .3f), Color.white, Mathf.Clamp01(sample.Elevation * 3));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, NightBlend);
        }

        public void Release()
        {
            if (!captured) return;
            if (ownedSun)
            {
                ownedSun.transform.rotation = originalRotation;
                ownedSun.intensity = originalIntensity;
                ownedSun.color = originalColor;
            }
            if (RenderSettings.sun == ownedSun)
            {
                RenderSettings.sun = originalSun;
                RenderSettings.ambientMode = originalMode;
                RenderSettings.ambientLight = originalAmbient;
            }
            captured = false;
            ownedSun = null;
        }

        private void OnDisable() => Release();
    }
}
