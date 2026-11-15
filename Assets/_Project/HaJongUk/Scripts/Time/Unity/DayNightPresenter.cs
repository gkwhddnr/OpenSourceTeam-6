using UnityEngine;
namespace OpenSourceTeam6.Time.Unity
{
    public sealed class DayNightPresenter : MonoBehaviour
    {
        [SerializeField] private BusinessTimeController timeSource;
        [SerializeField] private Light sun;
        [Tooltip("Art-directed August-November presets, not astronomical observations.")]
        [SerializeField, Range(8, 11)] private int month = 9;
        [SerializeField] private Vector2 august = new Vector2(5.5f, 19.5f);
        [SerializeField] private Vector2 september = new Vector2(6, 18.5f);
        [SerializeField] private Vector2 october = new Vector2(6.5f, 18);
        [SerializeField] private Vector2 november = new Vector2(7, 17.5f);
        [SerializeField, Min(0)] private float daylightIntensity = 1;
        [SerializeField] private Color nightAmbient = new Color(.08f, .1f, .16f);
        [SerializeField] private Color dayAmbient = new Color(.55f, .6f, .7f);
        public Vector3 SunDirection {
            get {
                Vector2 range = month == 8 ? august : month == 9 ? september : month == 10 ? october : november;
                if (!timeSource || range.y <= range.x || range.y-range.x >= 24) return Vector3.up;
                float hour = timeSource.GameHour;
                float angle = hour >= range.x && hour <= range.y ? (hour-range.x)/(range.y-range.x)*Mathf.PI :
                    Mathf.PI + ((hour < range.x ? hour+24 : hour)-range.y)/(24-range.y+range.x)*Mathf.PI;
                return new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),.15f).normalized;
            }
        }
        private Quaternion originalRotation;
        private float originalIntensity;
        private Color originalColor, originalAmbient;
        private UnityEngine.Rendering.AmbientMode originalMode;
        private bool captured;
        private Light originalSun;
        private void OnEnable()
        {
            if (!timeSource || !sun || sun.type != LightType.Directional)
            { Debug.LogError("Assign time source and a Directional Light.", this); enabled = false; return; }
            originalSun = RenderSettings.sun; RenderSettings.sun = sun;
            originalRotation = sun.transform.rotation; originalIntensity = sun.intensity;
            originalColor = sun.color; originalAmbient = RenderSettings.ambientLight;
            originalMode = RenderSettings.ambientMode; captured = true;
            timeSource.Clock.Changed += Refresh;
            Refresh();
        }
        private void Refresh()
        {
            if (!sun || !timeSource) return;
            Vector2 range = month == 8 ? august : month == 9 ? september : month == 10 ? october : november;
            if (!float.IsFinite(range.x) || !float.IsFinite(range.y) || range.x < 0 || range.y > 24 || range.y <= range.x) return;
            float fraction = Mathf.Clamp01((timeSource.GameHour - range.x) / (range.y - range.x));
            float elevation = Mathf.Max(0, Mathf.Sin(fraction * Mathf.PI));
            float hour = timeSource.GameHour;
            float angle = hour >= range.x && hour <= range.y ? fraction * Mathf.PI :
                Mathf.PI + ((hour < range.x ? hour + 24 : hour) - range.y) / (24 - range.y + range.x) * Mathf.PI;
            // +X east, +Y up, +Z north. Light rays point away from the sun.
            Vector3 direction = SunDirection;
            sun.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            sun.intensity = Mathf.Max(0, daylightIntensity) * elevation;
            sun.color = Color.Lerp(new Color(1, .55f, .3f), Color.white, Mathf.Clamp01(elevation * 3));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, elevation);
        }
        private void OnDisable()
        {
            if (timeSource) timeSource.Clock.Changed -= Refresh;
            if (!captured) return;
            if (sun) { sun.transform.rotation = originalRotation; sun.intensity = originalIntensity; sun.color = originalColor; }
            if (RenderSettings.sun == sun) RenderSettings.sun = originalSun;
            RenderSettings.ambientMode = originalMode; RenderSettings.ambientLight = originalAmbient;
            captured = false;
        }
    }
}
