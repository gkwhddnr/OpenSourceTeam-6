using System;
using UnityEngine;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Scene-local composition and frame order; not a service locator.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BusinessTimeController), typeof(DayNightPresenter), typeof(CelestialBodyPresenter))]
    [RequireComponent(typeof(TimeSkyExposure), typeof(RainWeatherController))]
    public sealed class TimeEnvironmentController : MonoBehaviour
    {
        [KoreanInspectorLabel("태양 광원"), SerializeField] private Light sunLight;
        [KoreanInspectorLabel("플레이어 카메라"), SerializeField] private Camera viewCamera;
        [KoreanInspectorLabel("비 안개 강도 (%)"), SerializeField, Range(0, 100)] private float rainFogPercent = 50;
        private BusinessTimeController time;
        private DayNightPresenter daylight;
        private CelestialBodyPresenter moon;
        private RainWeatherController weather;
        private TimeSkyExposure sky;
        private SkyFrame frame;
        private bool hasFrame;
        private readonly RainFogEffect fog = new RainFogEffect();

        public bool IsRain { get => weather && weather.isRain; set { if (weather) weather.isRain = value; } }
        public float RainIntensity => weather ? weather.Intensity : 0;
        public float CloudIntensity => weather ? weather.CloudIntensity : 0;
        public int VisibleDropCount => weather ? weather.VisibleDropCount : 0;
        public int VisibleSplashCount => weather ? weather.VisibleSplashCount : 0;
        public float RainFogPercent { get => rainFogPercent; set => rainFogPercent = float.IsFinite(value) ? Mathf.Clamp(value, 0, 100) : 0; }

        public void Configure(Light sun, Camera camera)
        {
            sunLight = sun;
            viewCamera = camera;
        }

        private void OnEnable()
        {
            time = GetComponent<BusinessTimeController>();
            daylight = GetComponent<DayNightPresenter>();
            moon = GetComponent<CelestialBodyPresenter>();
            weather = GetComponent<RainWeatherController>();
            sky = GetComponent<TimeSkyExposure>();
            if (!viewCamera) viewCamera = Camera.main;
            if (!sunLight) sunLight = RenderSettings.sun;
            if (!time || !daylight || !moon || !weather || !sky || !viewCamera || !sunLight || sunLight.type != LightType.Directional)
            {
                Debug.LogError("Time environment requires a local clock/presenters, a camera and a Directional Light.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!time || !viewCamera || !sunLight || sunLight.type != LightType.Directional || !daylight || !moon || !weather || !sky)
            {
                Debug.LogError("Time environment lost a required binding; releasing presentation.", this);
                enabled = false;
                return;
            }
            try
            {
                var sun = daylight.Evaluate(time.GameHour);
                var lunar = moon.Evaluate(time.GameHour);
                daylight.Render(sunLight, sun);
                moon.Render(lunar, sun.Direction);
                if (weather.isActiveAndEnabled)
                    weather.Advance(UnityEngine.Time.deltaTime, time.Clock.IsPaused, sun.NightBlend, viewCamera.transform.position);
                fog.Apply(gameObject.scene, weather.isActiveAndEnabled ? weather.Intensity * Mathf.Clamp01(RainFogPercent / 100f) : 0, sun.NightBlend);
                var sunDirection = new Vector3(sun.Direction.X, sun.Direction.Y, sun.Direction.Z);
                var moonDirection = new Vector3(lunar.X, lunar.Y, lunar.Z);
                frame = new SkyFrame(sunDirection, moonDirection, sun.NightBlend, moon.MoonAngularRadius,
                    weather.isActiveAndEnabled ? weather.CloudIntensity : 0, time.ElapsedGameHours);
                hasFrame = true;
            }
            catch (ArgumentOutOfRangeException error)
            {
                Debug.LogError("Invalid celestial settings: " + error.Message, this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            // Pause/rain simulation is already configured before Unity's particle phase;
            // only camera-relative sky placement waits for camera movement to finish.
            if (hasFrame && sky && viewCamera)
                sky.Render(frame, viewCamera.transform.position, viewCamera.farClipPlane);
        }

        public void SetViewRotation(Quaternion rotation)
        {
            if (viewCamera) viewCamera.transform.rotation = rotation;
        }

        private void OnDisable()
        {
            hasFrame = false;
            fog.Release(gameObject.scene);
            if (sky) sky.Release();
            if (moon) moon.Release();
            if (daylight) daylight.Release();
            if (weather) weather.Release();
        }
    }

    /// <summary>Owns only this scene's temporary fog override; restores the captured scene settings.</summary>
    internal sealed class RainFogEffect
    {
        private bool captured, originalEnabled;
        private FogMode originalMode;
        private float originalDensity, originalStart, originalEnd;
        private Color originalColor;

        public void Apply(UnityEngine.SceneManagement.Scene owner, float amount, float night)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene() != owner)
            {
                // A different scene now owns RenderSettings; never restore into it.
                captured = false;
                return;
            }
            amount = Mathf.Clamp01(amount);
            if (amount <= 0) { Release(owner); return; }
            if (!captured)
            {
                originalEnabled = RenderSettings.fog;
                originalMode = RenderSettings.fogMode;
                originalDensity = RenderSettings.fogDensity;
                originalStart = RenderSettings.fogStartDistance;
                originalEnd = RenderSettings.fogEndDistance;
                originalColor = RenderSettings.fogColor;
                captured = true;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = originalEnabled ? originalMode : FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(originalEnabled ? originalColor : Color.Lerp(new Color(.48f, .52f, .56f), new Color(.06f, .075f, .11f), night),
                Color.Lerp(new Color(.48f, .52f, .56f), new Color(.06f, .075f, .11f), night), amount);
            RenderSettings.fogDensity = Mathf.Lerp(originalEnabled ? originalDensity : 0, .025f, amount);
            if (originalEnabled && originalMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = Mathf.Lerp(originalStart, 5, amount);
                RenderSettings.fogEndDistance = Mathf.Lerp(originalEnd, 55, amount);
            }
        }

        public void Release(UnityEngine.SceneManagement.Scene owner)
        {
            if (!captured) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene() == owner)
            {
                RenderSettings.fog = originalEnabled;
                RenderSettings.fogMode = originalMode;
                RenderSettings.fogDensity = originalDensity;
                RenderSettings.fogStartDistance = originalStart;
                RenderSettings.fogEndDistance = originalEnd;
                RenderSettings.fogColor = originalColor;
            }
            captured = false;
        }
    }
}
