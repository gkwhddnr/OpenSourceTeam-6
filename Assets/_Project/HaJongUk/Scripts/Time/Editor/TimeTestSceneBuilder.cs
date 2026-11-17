using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OpenSourceTeam6.Time.Unity;

[InitializeOnLoad]
public static class TimeTestSceneBuilder
{
    private const string Request = "Temp/create-time-scene.request";
    private static double nextPoll;

    static TimeTestSceneBuilder()
    {
        EditorApplication.update += RunRequested;
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2;
        if (!File.Exists("Temp/time-refresh.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Temp/time-refresh.request");
        AssetDatabase.Refresh();
    }

    private static void RunRequested()
    {
        EditorApplication.update -= RunRequested;
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("Time scene creation waiting: save current scene first.");
                return;
            }
        File.Delete(Request);
        Build();
    }

    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Time Test Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 2, 0);
        camera.transform.rotation = Quaternion.Euler(-15, 90, 0);
        camera.farClipPlane = 1000;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.gameObject.AddComponent<AudioListener>();
        var sun = new GameObject("Sun Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        var root = new GameObject("Time System");
        root.SetActive(false);
        var time = root.AddComponent<BusinessTimeController>();
        var settings = new SerializedObject(time);
        settings.FindProperty("standaloneClock").boolValue = false;
        settings.FindProperty("openingHour").floatValue = 8;
        settings.FindProperty("closingHour").floatValue = 32;
        settings.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<DayNightPresenter>();
        root.AddComponent<CelestialBodyPresenter>();
        root.AddComponent<RainWeatherController>();
        root.AddComponent<TimeSkyExposure>();
        var environment = root.AddComponent<TimeEnvironmentController>();
        environment.Configure(sun, camera);
        root.AddComponent<TimeDemoPanel>();

        var sky = new Material(Shader.Find("Skybox/Procedural"));
        const string skyPath = "Assets/_Project/HaJongUk/Assets/TimeTestSky.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (existing)
        {
            EditorUtility.CopySerialized(sky, existing);
            Object.DestroyImmediate(sky);
            sky = existing;
        }
        else AssetDatabase.CreateAsset(sky, skyPath);
        RenderSettings.skybox = sky;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = Vector3.one * 10;
        root.SetActive(true);
        EditorSceneManager.SaveScene(scene, "Assets/_Project/HaJongUk/Scenes/TimeSystem_Test.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("TIME_TEST_SCENE_CREATED");
    }
}
