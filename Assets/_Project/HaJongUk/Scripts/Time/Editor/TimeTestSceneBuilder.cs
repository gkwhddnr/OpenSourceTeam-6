using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OpenSourceTeam6.Time.Unity;
using System.IO;
[InitializeOnLoad]
public static class TimeTestSceneBuilder {
 const string Request="Temp/create-time-scene.request";
 static TimeTestSceneBuilder(){ EditorApplication.update += RunRequested; EditorApplication.update += Poll; }
 static double nextPoll;
 static void Poll(){if(EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+2;if(File.Exists("Temp/time-refresh.request") && !EditorApplication.isPlayingOrWillChangePlaymode){File.Delete("Temp/time-refresh.request");AssetDatabase.Refresh();}}
 static void RunRequested(){
 EditorApplication.update -= RunRequested;
 if(!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
 for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) { Debug.LogWarning("Time scene creation waiting: save current scene first."); return; }
 File.Delete(Request); Build();
 }
 public static void Build(){
 var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 var cam=new GameObject("Time Test Camera").AddComponent<Camera>(); cam.tag="MainCamera"; cam.transform.position=new Vector3(0,2,0); cam.transform.rotation=Quaternion.Euler(-15,90,0); cam.farClipPlane=1000; cam.clearFlags=CameraClearFlags.Skybox; cam.gameObject.AddComponent<AudioListener>();
 var light=new GameObject("Sun Directional Light").AddComponent<Light>(); light.type=LightType.Directional; light.shadows=LightShadows.Soft; RenderSettings.sun=light;
 var root=new GameObject("Time System"); root.SetActive(false);
 var time=root.AddComponent<BusinessTimeController>(); var timeSo=new SerializedObject(time); timeSo.FindProperty("openingHour").floatValue=0; timeSo.FindProperty("closingHour").floatValue=24; timeSo.ApplyModifiedPropertiesWithoutUndo();
 var day=root.AddComponent<DayNightPresenter>(); Set(day,"timeSource",time); Set(day,"sun",light);
 var bodies=root.AddComponent<CelestialBodyPresenter>(); Set(bodies,"timeSource",time); Set(bodies,"sunLight",light); Set(bodies,"viewCamera",cam);
 var demo=root.AddComponent<TimeDemoPanel>(); demo.timeSource=time;demo.viewCamera=cam;
 var sky=new Material(Shader.Find("Skybox/Procedural"));
 const string skyPath="Assets/_Project/HaJongUk/Assets/TimeTestSky.mat";
 var existing=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
 if(existing){EditorUtility.CopySerialized(sky,existing);Object.DestroyImmediate(sky);sky=existing;}else AssetDatabase.CreateAsset(sky,skyPath);
 RenderSettings.skybox=sky;
 var skyCycle=root.AddComponent<TimeSkyExposure>();skyCycle.timeSource=time;
 var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Ground";ground.transform.localScale=Vector3.one*10;
 root.SetActive(true);
 EditorSceneManager.SaveScene(scene,"Assets/_Project/HaJongUk/Scenes/TimeSystem_Test.unity");AssetDatabase.SaveAssets();
 Debug.Log("TIME_TEST_SCENE_CREATED");
 }
 static void Set(Object target,string name,Object value){var so=new SerializedObject(target);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
}
