using OpenSourceTeam6.Time.Core;
using Unity.Profiling;
using UnityEngine;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Owns the target weather and transition; the environment supplies frame values.</summary>
    [DisallowMultipleComponent]
    public sealed class RainWeatherController : MonoBehaviour
    {
        [KoreanInspectorLabel("비 내리기"), Tooltip("목표 날씨를 설정합니다. 시작할 때 켜져 있어도 비가 서서히 시작됩니다.")]
        public bool isRain;
        [KoreanInspectorLabel("비 시작 전환 시간 (초)"), SerializeField, Min(.1f)] private float rainInSeconds = 12;
        [KoreanInspectorLabel("비 그침 전환 시간 (초)"), SerializeField, Min(.1f)] private float rainOutSeconds = 10;
        [KoreanInspectorLabel("초당 최대 빗방울 수"), SerializeField, Range(0, 6400)] private float maximumDropsPerSecond = 1400;
        [KoreanInspectorLabel("빗방울 생성 상한"), SerializeField, Range(0, 6400), Tooltip("초당 생성량의 상한입니다. 넓은 영역에서는 성능을 확인한 뒤 높일 수 있습니다.")]
        private float emissionBudget = 1600;
        [Header("비가 내리는 영역")]
        [KoreanInspectorLabel("고정 영역 사용"), SerializeField, Tooltip("카메라를 따라가지 않고 월드 좌표의 고정 영역에 비를 내립니다.")]
        private bool useFixedRainArea;
        [KoreanInspectorLabel("비 영역 크기"), SerializeField, Tooltip("월드 단위의 가로(X)와 세로(Z) 크기입니다.")]
        private Vector2 rainAreaSize = new Vector2(30, 30);
        [KoreanInspectorLabel("고정 영역 중심"), SerializeField, Tooltip("고정 영역에서 빗방울이 생성되는 월드 좌표입니다. Y는 생성 높이입니다.")]
        private Vector3 fixedRainCenter = new Vector3(0, 12, 0);
        [KoreanInspectorLabel("카메라 위 생성 높이"), SerializeField, Min(0), Tooltip("카메라 추적 모드에서 카메라 위로 얼마나 높게 비를 생성할지 설정합니다.")]
        private float cameraHeightOffset = 12;
        [KoreanInspectorLabel("최대 빗방울 낙하 거리"), SerializeField, Min(1), Tooltip("빗방울이 사라지기 전 최대 낙하 거리입니다. 생성 지점부터 가장 낮은 바닥까지의 거리 이상으로 설정하세요.")]
        private float rainFallDistance = 20;
        [KoreanInspectorLabel("비 충돌 레이어"), Tooltip("고정된 바닥과 지붕의 레이어를 선택합니다. 플레이어와 직원 레이어는 제외하세요.")]
        [SerializeField] private LayerMask rainCollisionLayers = Physics.DefaultRaycastLayers;
        [KoreanInspectorLabel("물튀김 발생 확률"), SerializeField, Range(0, 1)] private float splashProbability = .22f;

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
                float budget = float.IsFinite(emissionBudget) ? Mathf.Clamp(emissionBudget, 0, 6400) : 0;
                float rate = float.IsFinite(maximumDropsPerSecond)
                    ? Mathf.Clamp(maximumDropsPerSecond, 0, budget) * Intensity : 0;
                Vector2 size = SafeAreaSize();
                Vector3 center = useFixedRainArea && IsFinite(fixedRainCenter)
                    ? fixedRainCenter
                    : cameraPosition + Vector3.up * (float.IsFinite(cameraHeightOffset) ? Mathf.Max(0, cameraHeightOffset) : 12);
                float fallDistance = float.IsFinite(rainFallDistance) ? Mathf.Clamp(rainFallDistance, 1, 100) : 20;
                effect.Apply(rate, budget, paused, night, center, size, useFixedRainArea, fallDistance);
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

        private Vector2 SafeAreaSize() => new Vector2(
            float.IsFinite(rainAreaSize.x) ? Mathf.Clamp(rainAreaSize.x, 1, 1000) : 30,
            float.IsFinite(rainAreaSize.y) ? Mathf.Clamp(rainAreaSize.y, 1, 1000) : 30);

        private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private void OnDrawGizmosSelected()
        {
            if (!useFixedRainArea || !IsFinite(fixedRainCenter)) return;
            Vector2 size = SafeAreaSize();
            Gizmos.color = new Color(.3f, .65f, 1, .8f);
            Gizmos.DrawWireCube(fixedRainCenter, new Vector3(size.x, .5f, size.y));
        }

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
