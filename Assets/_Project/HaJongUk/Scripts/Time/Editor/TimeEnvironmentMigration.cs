using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OpenSourceTeam6.Time.Unity;

/// <summary>Explicit, idempotent migration of the two owned scene compositions.</summary>
public static class TimeEnvironmentMigration
{
    public const string TestScene = "Assets/_Project/HaJongUk/Scenes/TimeSystem_Test.unity";
    public const string TeamScene = "Assets/_Project/Shared/Scenes/Team_Integration.unity";

    public static string Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before migration.");
        Scene original = SceneManager.GetActiveScene();
        if (original.path != TestScene) throw new InvalidOperationException("Open the time test scene first.");
        Directory.CreateDirectory("output/refactor-backup");
        if (!EditorSceneManager.SaveScene(original, "output/refactor-backup/TimeSystem_Test.pre-migration.unity", true))
            throw new InvalidOperationException("Could not preserve the current scene.");
        string report = MigrateScene(original);
        Scene team = SceneManager.GetSceneByPath(TeamScene);
        bool openedHere = !team.IsValid() || !team.isLoaded;
        if (openedHere) team = EditorSceneManager.OpenScene(TeamScene, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(team);
            if (!EditorSceneManager.SaveScene(team, "output/refactor-backup/Team_Integration.pre-migration.unity", true))
                throw new InvalidOperationException("Could not preserve team scene.");
            report += "\n" + MigrateScene(team);
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            if (openedHere) EditorSceneManager.CloseScene(team, true);
        }
        File.WriteAllText("output/time-refactor-migration.txt", report);
        return report;
    }

    private static string MigrateScene(Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).Where(c => c).ToArray();
        var clock = all.OfType<BusinessTimeController>().Single();
        var camera = all.OfType<Camera>().Single(c => c.CompareTag("MainCamera"));
        Light sun = RenderSettings.sun;
        if (!sun || sun.gameObject.scene != scene)
            sun = all.OfType<Light>().Single(l => l.type == LightType.Directional);
        var environment = clock.GetComponent<TimeEnvironmentController>();
        if (!environment) environment = Undo.AddComponent<TimeEnvironmentController>(clock.gameObject);
        Undo.RecordObject(environment, "Migrate time environment bindings");
        environment.Configure(sun, camera);
        EditorUtility.SetDirty(environment);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save migrated scene " + scene.path);
        int count = CountReferences(clock.gameObject);
        return scene.path + ": serialized component reference fields=" + count + ", environment=" + environment.name +
            ", camera=" + camera.name + ", sun=" + sun.name;
    }

    public static int CountReferences(GameObject root)
    {
        Type[] types = { typeof(DayNightPresenter), typeof(CelestialBodyPresenter), typeof(TimeSkyExposure),
            typeof(RainWeatherController), typeof(TimeDemoPanel), typeof(TimeEnvironmentController) };
        return root.GetComponents<MonoBehaviour>().Where(c => c && types.Contains(c.GetType())).Sum(c =>
            c.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Count(f => typeof(Component).IsAssignableFrom(f.FieldType) && (f.IsPublic || f.IsDefined(typeof(SerializeField), false))));
    }
}
