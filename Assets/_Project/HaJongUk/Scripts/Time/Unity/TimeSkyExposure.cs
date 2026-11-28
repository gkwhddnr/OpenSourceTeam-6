using UnityEngine;
using UnityEngine.Rendering;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Owns sky resources and consumes frame values, without sibling lookups.</summary>
    [DisallowMultipleComponent]
    public sealed class TimeSkyExposure : MonoBehaviour
    {
        [KoreanInspectorLabel("구름 이동 속도"), Tooltip("게임 시간 한 시간당 구름이 이동하는 각도입니다."), SerializeField] private float cloudDegreesPerGameHour = 2;
        private Material original, instance, overlay;
        private GameObject dome;
        private bool failed;
        private static readonly int NightId = Shader.PropertyToID("_Night");
        private static readonly int DriftId = Shader.PropertyToID("_Drift");
        private static readonly int RainId = Shader.PropertyToID("_Rain");
        private static readonly int MoonDirectionId = Shader.PropertyToID("_MoonDirection");
        private static readonly int MoonRadiusId = Shader.PropertyToID("_MoonRadius");
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int SunsetGlowId = Shader.PropertyToID("_SunsetGlow");

        public void Render(SkyFrame frame, Vector3 cameraPosition, float farClipPlane)
        {
            if (!isActiveAndEnabled || failed) return;
            if (!overlay && !CreateResources()) return;
            overlay.SetFloat(NightId, frame.NightBlend);
            overlay.SetFloat(SunsetGlowId, frame.SunsetGlow);
            overlay.SetFloat(DriftId, frame.ElapsedGameHours * cloudDegreesPerGameHour * .05f);
            overlay.SetFloat(RainId, frame.CloudIntensity);
            overlay.SetVector(MoonDirectionId, frame.MoonDirection);
            overlay.SetFloat(MoonRadiusId, frame.MoonRadius);
            overlay.SetVector(SunDirectionId, frame.SunDirection);
            dome.transform.position = cameraPosition;
            dome.transform.localScale = Vector3.one * farClipPlane;
        }

        private bool CreateResources()
        {
            var shader = Shader.Find("Skybox/Procedural");
            var layer = Resources.Load<Shader>("TimeCelestials/SkyOverlay");
            if (!shader || !layer)
            {
                failed = true;
                Debug.LogError("Time sky requires Procedural and SkyOverlay shaders.", this);
                return false;
            }
            original = RenderSettings.skybox;
            instance = new Material(shader) { name = "Time sky (runtime)" };
            instance.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            instance.SetFloat("_SunDisk", 2);
            instance.SetFloat("_SunSize", .04f);
            instance.SetFloat("_Exposure", 1.3f);
            RenderSettings.skybox = instance;
            overlay = new Material(layer) { name = "Clouds and stars (runtime)" };
            dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Clouds and stars overlay";
            var collider = dome.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = dome.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = overlay;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return true;
        }

        public void Release()
        {
            if (instance && RenderSettings.skybox == instance) RenderSettings.skybox = original;
            if (dome) Destroy(dome);
            if (instance) Destroy(instance);
            if (overlay) Destroy(overlay);
            dome = null; instance = null; overlay = null; original = null;
            failed = false;
        }

        private void OnDisable() => Release();
    }
}
