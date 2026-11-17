using UnityEngine;
using OpenSourceTeam6.Time.Core;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Owns lunar art settings and moonlight; the sky renderer draws the disk.</summary>
    [DisallowMultipleComponent]
    public sealed class CelestialBodyPresenter : MonoBehaviour
    {
        [KoreanInspectorLabel("달의 겉보기 지름 (도)"), SerializeField, Range(.1f, 10)] private float moonDiameterDegrees = 2;
        [KoreanInspectorLabel("달이 뜨는 시각"), Tooltip("연출용 달 뜨는 시각입니다. 실제 달의 궤도는 아닙니다.")]
        [SerializeField, Range(0, 24)] private float moonriseHour = 17;
        [KoreanInspectorLabel("달이 보이는 시간"), SerializeField, Range(1, 23)] private float moonVisibleHours = 12;
        [KoreanInspectorLabel("달빛 강도"), SerializeField, Min(0)] private float moonlightIntensity = .12f;
        private Light moonLight;
        public Vector3 MoonDirection { get; private set; } = Vector3.down;
        public float MoonAngularRadius => moonDiameterDegrees * Mathf.Deg2Rad * .5f;

        public CelestialDirection Evaluate(float gameHour) => CelestialMath.SampleMoon(gameHour, moonriseHour, moonVisibleHours);

        public void Render(CelestialDirection moon, CelestialDirection sun)
        {
            if (!isActiveAndEnabled) return;
            if (!moonLight)
            {
                var lightObject = new GameObject("Moon Directional Light (runtime)");
                lightObject.transform.SetParent(transform, false);
                moonLight = lightObject.AddComponent<Light>();
                moonLight.type = LightType.Directional;
                moonLight.color = new Color(.65f, .75f, 1);
                moonLight.shadows = LightShadows.None;
            }
            MoonDirection = new Vector3(moon.X, moon.Y, moon.Z);
            moonLight.transform.rotation = Quaternion.LookRotation(-MoonDirection, Vector3.up);
            moonLight.intensity = moonlightIntensity * Mathf.Clamp01(moon.Y * 3) * (1 - Mathf.Clamp01(sun.Y * 5));
        }

        public void Release()
        {
            if (moonLight) Destroy(moonLight.gameObject);
            moonLight = null;
            MoonDirection = Vector3.down;
        }

        private void OnDisable() => Release();
    }
}
