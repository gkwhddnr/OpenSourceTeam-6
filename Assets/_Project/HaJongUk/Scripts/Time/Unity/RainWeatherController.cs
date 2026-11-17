using OpenSourceTeam6.Time.Core;
using Unity.Profiling;
using UnityEngine;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Owns the target weather and transition; the environment supplies frame values.</summary>
    [DisallowMultipleComponent]
    public sealed class RainWeatherController : MonoBehaviour
    {
        [Tooltip("Target weather. Changes fade gradually, including an initially checked value.")]
        public bool isRain;
        [SerializeField, Min(.1f)] private float rainInSeconds = 12;
        [SerializeField, Min(.1f)] private float rainOutSeconds = 10;
        [SerializeField, Range(0, 1600)] private float maximumDropsPerSecond = 1400;
        [Tooltip("Static ground and roofs. Exclude player/employee layers when the scene defines them.")]
        [SerializeField] private LayerMask rainCollisionLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Range(0, 1)] private float splashProbability = .22f;

        public float Intensity => transition.RainIntensity;
        public float CloudIntensity => transition.CloudIntensity;
        public int VisibleDropCount => effect?.VisibleDropCount ?? 0;
        public int VisibleSplashCount => effect?.VisibleSplashCount ?? 0;

        private RainTransition transition = new RainTransition();
        private RainParticleEffect effect;
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("HaJongUk.RainWeather.Update");

        private void OnEnable()
        {
            transition = new RainTransition();
            // The composition owner starts presentation on its first frame. A disabled
            // environment must not leave an independently playing particle system.
        }

        /// <summary>Called once by the environment owner, using scaled real seconds.</summary>
        public void Advance(float seconds, bool paused, float night, Vector3 cameraPosition)
        {
            using (UpdateMarker.Auto())
            {
                if (!isActiveAndEnabled || !EnsureEffect()) return;
                transition.Advance(isRain, paused ? 0 : seconds,
                    SafeDuration(rainInSeconds), SafeDuration(rainOutSeconds));
                float rate = float.IsFinite(maximumDropsPerSecond)
                    ? Mathf.Clamp(maximumDropsPerSecond, 0, 1600) * Intensity : 0;
                effect.Apply(rate, paused, night, cameraPosition);
            }
        }

        private bool EnsureEffect()
        {
            if (effect != null) return true;
            if (RainParticleEffect.TryCreate(transform, rainCollisionLayers, splashProbability, out effect)) return true;
            Debug.LogError("Rain weather requires the RainStreak and RainSplash shaders.", this);
            enabled = false;
            return false;
        }

        private static float SafeDuration(float value) => float.IsFinite(value) ? Mathf.Max(.1f, value) : 12;

        /// <summary>Releases owned presentation without changing the target or component state.</summary>
        public void Release()
        {
            effect?.Dispose();
            effect = null;
            transition = new RainTransition();
        }

        private void OnDisable() => Release();
    }
}
