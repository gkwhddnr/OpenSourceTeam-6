using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using OpenSourceTeam6.Time.Unity;

// Explicit verification only; never runs just because the Editor imports this script.
[InitializeOnLoad]
public static class RainWeatherVerification
{
    private const string ReportKey = "HaJongUk.RainVerification.Report";
    private static readonly List<double> Frames = new List<double>(6000);
    private static RainWeatherController weather;
    private static BusinessTimeController clock;
    private static double started, previous;
    private static int lastFrame, maximumDrops, maximumSplashes, stage;
    private static bool pausedStable, drained, cleanup;
    private static int pausedDrops, pausedSplashes;
    private static bool originalBackground;
    private static ProfilerRecorder rainUpdate, simulation, collection;
    private static long maximumRainNs, maximumSimulationNs, maximumGcNs;
    private static int measuredGcStart, measuredGcEnd;
    private static TimeEnvironmentController environment;
    private static bool environmentCleanup, environmentRecreated;

    static RainWeatherVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (SessionState.GetString(ReportKey, "") != "") EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch Editor.");
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-rainReport");
        EditorSceneManager.OpenScene("Assets/_Project/HaJongUk/Scenes/TimeSystem_Test.unity");
        RunLive(index >= 0 ? args[index + 1] : "output/rain-verification.txt");
    }

    public static void RunLive(string report)
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            (scene != "Assets/_Project/HaJongUk/Scenes/TimeSystem_Test.unity" && scene != "Assets/_Project/Shared/Scenes/Team_Integration.unity"))
            throw new InvalidOperationException("Open the time test or team integration scene in Edit Mode first.");
        SessionState.SetString(ReportKey, report);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (SessionState.GetString(ReportKey, "") == "") return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Frames.Clear();
            originalBackground = Application.runInBackground;
            Application.runInBackground = true;
            weather = UnityEngine.Object.FindAnyObjectByType<RainWeatherController>();
            clock = UnityEngine.Object.FindAnyObjectByType<BusinessTimeController>();
            environment = clock.GetComponent<TimeEnvironmentController>();
            environmentCleanup = environmentRecreated = false;
            var settings = new SerializedObject(weather);
            settings.FindProperty("rainInSeconds").floatValue = .2f;
            settings.FindProperty("rainOutSeconds").floatValue = .2f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            weather.isRain = true;
            var demo = UnityEngine.Object.FindAnyObjectByType<TimeDemoPanel>();
            if (demo) demo.fullDayDurationSeconds = 600;
            started = previous = EditorApplication.timeSinceStartup;
            stage = maximumDrops = maximumSplashes = 0;
            measuredGcStart = -1;
            maximumRainNs = maximumSimulationNs = maximumGcNs = 0;
            rainUpdate = StartRecorder("HaJongUk.RainWeather.Update");
            simulation = StartRecorder("ParticleSystem.Simulate");
            collection = StartRecorder("GC.Collect");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(ReportKey);
            Application.runInBackground = originalBackground;
            rainUpdate.Dispose(); simulation.Dispose(); collection.Dispose();
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }

    private static int Splashes => (int)(typeof(RainWeatherController)
        .GetProperty("VisibleSplashCount")?.GetValue(weather) ?? 0);

    private static void Tick()
    {
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || !weather || lastFrame == UnityEngine.Time.frameCount) return;
        lastFrame = UnityEngine.Time.frameCount;
        double now = EditorApplication.timeSinceStartup, elapsed = now - started;
        if (elapsed > 3 && elapsed < 18)
        {
            if (measuredGcStart < 0) measuredGcStart = GC.CollectionCount(0);
            measuredGcEnd = GC.CollectionCount(0);
            Frames.Add((now - previous) * 1000);
            maximumRainNs = Math.Max(maximumRainNs, rainUpdate.Valid ? rainUpdate.LastValue : 0);
            maximumSimulationNs = Math.Max(maximumSimulationNs, simulation.Valid ? simulation.LastValue : 0);
            maximumGcNs = Math.Max(maximumGcNs, collection.Valid ? collection.LastValue : 0);
        }
        previous = now;
        maximumDrops = Math.Max(maximumDrops, weather.VisibleDropCount);
        maximumSplashes = Math.Max(maximumSplashes, Splashes);
        if (elapsed > 18 && stage == 0)
        {
            Capture();
            clock.Clock.IsPaused = true;
            pausedDrops = weather.VisibleDropCount; pausedSplashes = Splashes;
            stage = 1;
        }
        if (elapsed > 19 && stage == 1)
        {
            pausedStable = pausedDrops == weather.VisibleDropCount && pausedSplashes == Splashes;
            clock.Clock.IsPaused = false; weather.isRain = false;
            stage = 2;
        }
        if (elapsed > 22 && stage == 2)
        {
            drained = weather.VisibleDropCount == 0 && Splashes == 0 && weather.Intensity == 0;
            weather.enabled = false; stage = 3;
        }
        if (elapsed > 22.5 && stage == 3)
        {
            cleanup = GameObject.Find("Rain drops (runtime)") == null && GameObject.Find("Rain splashes (runtime)") == null;
            weather.enabled = true; weather.isRain = true; stage = 4;
        }
        if (elapsed > 25 && stage == 4)
        {
            environment.enabled = false;
            stage = 5;
        }
        if (elapsed > 25.5 && stage == 5)
        {
            environmentCleanup = GameObject.Find("Rain drops (runtime)") == null &&
                GameObject.Find("Clouds and stars overlay") == null && GameObject.Find("Moon Directional Light (runtime)") == null;
            environment.enabled = true;
            stage = 6;
        }
        if (elapsed > 28 && stage == 6)
        {
            environmentRecreated = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length == 2 &&
                GameObject.Find("Clouds and stars overlay") != null && GameObject.Find("Moon Directional Light (runtime)") != null;
            Frames.Sort();
            int gc = measuredGcEnd - measuredGcStart;
            string path = SessionState.GetString(ReportKey, "output/rain-verification.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, $"Unity={Application.unityVersion}; GPU={SystemInfo.graphicsDeviceName}; API={SystemInfo.graphicsDeviceType}; Pipeline={GraphicsSettings.currentRenderPipeline?.name}; Editor, not Player\n" +
                $"samples={Frames.Count}; frame p50/p95/p99/max ms={Percentile(.5):F2}/{Percentile(.95):F2}/{Percentile(.99):F2}/{Percentile(1):F2}; GC0={gc}\n" +
                $"max rain Update/synchronous Simulate/GC.Collect ms={maximumRainNs/1e6:F4}/{maximumSimulationNs/1e6:F4}/{maximumGcNs/1e6:F4}; frames over 33ms={Frames.FindAll(t=>t>33.33).Count}\n" +
                $"max drops={maximumDrops}; max splashes={maximumSplashes}; pause stable={pausedStable}; drained={drained}; cleanup={cleanup}; repeat play particles={weather.VisibleDropCount}\n" +
                $"environment cleanup={environmentCleanup}; environment recreated once={environmentRecreated}\n");
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }
    }

    private static double Percentile(double fraction) => Frames.Count == 0 ? 0 : Frames[Math.Min(Frames.Count - 1, (int)(Frames.Count * fraction))];

    private static ProfilerRecorder StartRecorder(string name)
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var handle in handles)
        {
            var description = ProfilerRecorderHandle.GetDescription(handle);
            if (description.Name == name) return ProfilerRecorder.StartNew(description.Category, name);
        }
        return default;
    }

    private static void Capture()
    {
        var camera = Camera.main;
        camera.transform.position = new Vector3(0, 2, -5);
        camera.transform.rotation = Quaternion.Euler(22, 0, 0);
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        RenderTexture old = RenderTexture.active, oldTarget = camera.targetTexture;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.ChangeExtension(SessionState.GetString(ReportKey, ""), ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(texture);
        }
    }
}
