using UnityEngine;
using UnityEngine.Rendering;
namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Art-directed sky spheres. Not an astronomical ephemeris.</summary>
    public sealed class CelestialBodyPresenter : MonoBehaviour
    {
        [SerializeField] private BusinessTimeController timeSource;
        [SerializeField] private Light sunLight;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Range(.1f, 10)] private float sunDiameterDegrees = 1.2f;
        [SerializeField, Range(.1f, 10)] private float moonDiameterDegrees = 2;
        [Tooltip("Independent art-directed lunar rise time, not a real lunar orbit.")]
        [SerializeField, Range(0, 24)] private float moonriseHour = 17;
        [SerializeField, Range(1, 23)] private float moonVisibleHours = 12;
        [SerializeField, Min(0)] private float moonlightIntensity = .12f;
        private Light moonLight;
        public Vector3 MoonDirection { get; private set; } = Vector3.down;
        public float MoonAngularRadius => moonDiameterDegrees * Mathf.Deg2Rad * .5f;
        private GameObject sunBody, moonBody;
        private DayNightPresenter daylight;
        private Material sunMaterial, moonMaterial;
        private static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
        private void OnEnable()
        {
            daylight = GetComponent<DayNightPresenter>();
            if (!viewCamera) viewCamera = Camera.main;
            Shader shader = Resources.Load<Shader>("TimeCelestials/Celestial");
            Texture2D moonTexture = Texture2D.whiteTexture;
            if (!timeSource || !sunLight || !viewCamera || !shader || !moonTexture)
            { Debug.LogError($"Celestial missing dependency: time={timeSource}, light={sunLight}, camera={viewCamera}, shader={shader}, moonTexture={moonTexture}", this); enabled = false; return; }
            // Sun disk is rendered once by Unity Procedural Skybox.
            // Moon is a luminous disk with no surface texture.
            moonBody = CreateBody("Moon visual", shader, moonTexture, false, out moonMaterial);
            // SkyOverlay draws an analytic, derivative-antialiased disk behind clouds.
            moonBody.GetComponent<MeshRenderer>().enabled = false;
            moonBody.SetActive(false);
            var lightObject = new GameObject("Moon Directional Light (runtime)");
            moonLight = lightObject.AddComponent<Light>(); moonLight.type = LightType.Directional; moonLight.color = new Color(.65f,.75f,1); moonLight.shadows = LightShadows.None;
            timeSource.Clock.Changed += RefreshBodies;
            RefreshBodies();
        }
        private GameObject CreateBody(string bodyName, Shader shader, Texture texture, bool isSun, out Material material)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = bodyName;
            body.layer = gameObject.layer;
            // Owned world-space objects avoid inherited scale distorting angular diameter.
            var collider = body.GetComponent<Collider>();
            collider.enabled = false; Destroy(collider);
            material = new Material(shader) { name = bodyName + " (runtime)" };
            material.mainTexture = texture; material.SetFloat("_IsSun", isSun ? 1 : 0);
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return body;
        }
        private void LateUpdate() { RefreshBodies(); }
        private void RefreshBodies()
        {
            if (!viewCamera || !sunLight || !timeSource || !moonBody) return;
            Vector3 sunDirection = daylight ? daylight.SunDirection : -sunLight.transform.forward;
            float sinceRise = Mathf.Repeat(timeSource.GameHour - moonriseHour, 24);
            float angle = sinceRise <= moonVisibleHours ? sinceRise / moonVisibleHours * Mathf.PI :
                Mathf.PI + (sinceRise - moonVisibleHours) / (24 - moonVisibleHours) * Mathf.PI;
            Vector3 moonDirection = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -.25f).normalized;

            MoonDirection = moonDirection;
            Place(moonBody, moonDirection, moonDiameterDegrees);
            if (moonLight) { moonLight.transform.rotation = Quaternion.LookRotation(-moonDirection, Vector3.up); moonLight.intensity = moonlightIntensity * Mathf.Clamp01(moonDirection.y*3) * (1-Mathf.Clamp01(sunDirection.y*5)); }
            moonMaterial.SetVector(SunDirection, sunDirection);
        }
        private void Place(GameObject body, Vector3 direction, float diameter)
        {
            body.SetActive(direction.y > 0);
            // Camera-centred spheres have no positional parallax and remain inside the far plane.
            float distance = viewCamera.farClipPlane * .85f;
            body.transform.position = viewCamera.transform.position + direction * distance;
            body.transform.localScale = Vector3.one * (2 * distance * Mathf.Tan(diameter * Mathf.Deg2Rad * .5f));
        }
        private void OnDisable()
        {
            if (timeSource) timeSource.Clock.Changed -= RefreshBodies;
            if (sunBody) Destroy(sunBody);
            if (moonBody) Destroy(moonBody);
            if (moonLight) Destroy(moonLight.gameObject);
            if (sunMaterial) Destroy(sunMaterial);
            if (moonMaterial) Destroy(moonMaterial);
        }
    }
}
