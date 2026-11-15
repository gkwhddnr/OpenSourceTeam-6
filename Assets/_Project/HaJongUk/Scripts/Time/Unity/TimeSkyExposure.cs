using UnityEngine;
namespace OpenSourceTeam6.Time.Unity {
 [DefaultExecutionOrder(100)]
 public sealed class TimeSkyExposure : MonoBehaviour {
 public BusinessTimeController timeSource;
 [SerializeField] private float cloudDegreesPerGameHour=2;
 private Material original,instance,overlay;
 private GameObject dome; private Camera cameraSource;private DayNightPresenter daylight;
 private void OnEnable(){
 original=RenderSettings.skybox; daylight=GetComponent<DayNightPresenter>();cameraSource=Camera.main;
 var shader=Shader.Find("Skybox/Procedural");var layer=Resources.Load<Shader>("TimeCelestials/SkyOverlay");
 if(!timeSource||!shader||!layer||!cameraSource)return;
 instance=new Material(shader);instance.EnableKeyword("_SUNDISK_HIGH_QUALITY");instance.SetFloat("_SunDisk",2);instance.SetFloat("_SunSize",.04f);RenderSettings.skybox=instance;
 overlay=new Material(layer);dome=GameObject.CreatePrimitive(PrimitiveType.Sphere);dome.name="Clouds and stars overlay";var collider=dome.GetComponent<Collider>();collider.enabled=false;Destroy(collider);var renderer=dome.GetComponent<MeshRenderer>();renderer.sharedMaterial=overlay;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
 timeSource.Clock.Changed+=Refresh;Refresh();}
 private void Refresh(){if(!overlay||!timeSource)return;float altitude=daylight?daylight.SunDirection.y:1;
 float night=daylight ? daylight.NightBlend : 0;overlay.SetFloat("_Night",night);overlay.SetFloat("_Drift",timeSource.ElapsedGameHours*cloudDegreesPerGameHour*.05f);instance.SetFloat("_Exposure",1.3f);}
 private void LateUpdate(){var bodies=GetComponent<CelestialBodyPresenter>();if(overlay && bodies){overlay.SetVector("_MoonDirection",bodies.MoonDirection);overlay.SetFloat("_MoonRadius",bodies.MoonAngularRadius);}if(overlay && daylight)overlay.SetVector("_SunDirection",daylight.SunDirection);if(dome&&cameraSource){dome.transform.position=cameraSource.transform.position;dome.transform.localScale=Vector3.one*cameraSource.farClipPlane;}}
 private void OnDisable(){if(timeSource)timeSource.Clock.Changed-=Refresh;if(RenderSettings.skybox==instance)RenderSettings.skybox=original;if(dome)Destroy(dome);if(instance)Destroy(instance);if(overlay)Destroy(overlay);}
 }
}
