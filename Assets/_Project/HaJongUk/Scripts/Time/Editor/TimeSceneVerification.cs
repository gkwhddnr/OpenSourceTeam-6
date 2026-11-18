using System.IO;
using UnityEditor;
using UnityEngine;
using OpenSourceTeam6.Time.Unity;

[InitializeOnLoad]
public static class TimeSceneVerification
{
    private static double next;
    private static int index;
    // Ascending progress through an 08:00 -> next 08:00 day, including next dawn.
    private static readonly float[] Hours = { 8, 12, 18, 19, 22, 5.5f, 6.5f };

    static TimeSceneVerification()
    {
        EditorApplication.update += Start;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && File.Exists("Temp/verify-time-scene.request"))
                EditorApplication.update += Start;
        };
    }

    private static void Start()
    {
        EditorApplication.update -= Start;
        if (File.Exists("Temp/create-time-scene.request"))
        {
            EditorApplication.update += Start;
            return;
        }
        if (File.Exists("Temp/verify-time-scene.request"))
        {
            File.Delete("Temp/verify-time-scene.request");
            index = 0;
            SessionState.SetBool("TimeVerify", true);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }
        if (!SessionState.GetBool("TimeVerify", false)) return;
        next = EditorApplication.timeSinceStartup + 4;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < next) return;
        var time = Object.FindAnyObjectByType<BusinessTimeController>();
        var demo = Object.FindAnyObjectByType<TimeDemoPanel>();
        var camera = Camera.main;
        if (!time || !demo || !camera || index >= Hours.Length * 2)
        {
            Finish();
            return;
        }
        demo.enabled = false;
        if (index == 0)
        {
            // The demo may have advanced while entering Play Mode. Start a fresh
            // day before sampling 08:00 so monotonic progress never rejects it.
            time.Clock.IsPaused = false;
            time.ApplyExternalProgress(time.Clock.DayId, 1);
            time.Clock.ConfirmSettlement(time.Clock.DayId);
            time.BeginDay(time.Clock.DayId + 1);
        }
        float hour = Hours[index / 2];
        if (index % 2 == 0)
        {
            if (time.Clock.Phase == OpenSourceTeam6.Time.Core.BusinessPhase.Ready) time.BeginDay(1);
            time.ApplyExternalProgress(time.Clock.DayId, Mathf.Repeat(hour - 8, 24) / 24f);
            camera.transform.rotation = hour < 6 ? Quaternion.Euler(-12, 90, 0)
                : Quaternion.Euler(-12, hour < 17 ? 90 : 270, 0);
            if (hour == 12) camera.transform.rotation = Quaternion.Euler(-75, 90, 0);
        }
        else
        {
            var moon = time.GetComponent<CelestialBodyPresenter>();
            if (hour >= 19 && moon && moon.MoonDirection.sqrMagnitude > .1f)
                camera.transform.rotation = Quaternion.LookRotation(moon.MoonDirection, Vector3.up);
            Capture(camera, time, moon, hour);
        }
        index++;
        next = EditorApplication.timeSinceStartup + 1;
    }

    private static void Capture(Camera camera, BusinessTimeController time, CelestialBodyPresenter moon, float requestedHour)
    {
        Directory.CreateDirectory("output/time-verification");
        const string results = "output/time-verification/results.txt";
        if (index == 1) File.WriteAllText(results, "");
        Vector3 direction = moon ? moon.MoonDirection : Vector3.down;
        File.AppendAllText(results, $"requestedHour={requestedHour} hour={time.GameHour} moonDirection={direction}\n");
        var target = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            string hourText = requestedHour.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllBytes("output/time-verification/hour-" + hourText + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(image);
        }
    }

    private static void Finish()
    {
        SessionState.SetBool("TimeVerify", false);
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
        Debug.Log("TIME_VISUAL_VERIFICATION_FINISHED");
    }
}
