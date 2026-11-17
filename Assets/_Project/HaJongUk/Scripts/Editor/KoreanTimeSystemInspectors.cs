using System.Reflection;
using OpenSourceTeam6.Time.Unity;
using UnityEditor;
using UnityEngine;

/// <summary>Displays localized labels without changing serialized field names or drawers.</summary>
public abstract class KoreanTimeSystemInspector : Editor
{
    protected abstract string KoreanTitle { get; }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField(KoreanTitle, EditorStyles.boldLabel);
        var property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            string label = property.name == "m_Script" ? "스크립트" : property.displayName;
            var field = target.GetType().GetField(property.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var localized = field?.GetCustomAttribute<KoreanInspectorLabelAttribute>();
            if (localized != null) label = localized.Label;
            using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(BusinessTimeController)), CanEditMultipleObjects]
public sealed class KoreanBusinessTimeInspector : KoreanTimeSystemInspector
{ protected override string KoreanTitle => "영업 시간"; }

[CustomEditor(typeof(DayNightPresenter)), CanEditMultipleObjects]
public sealed class KoreanDayNightInspector : KoreanTimeSystemInspector
{ protected override string KoreanTitle => "낮과 밤·태양"; }

[CustomEditor(typeof(CelestialBodyPresenter)), CanEditMultipleObjects]
public sealed class KoreanCelestialInspector : KoreanTimeSystemInspector
{ protected override string KoreanTitle => "달과 달빛"; }

[CustomEditor(typeof(TimeDemoPanel)), CanEditMultipleObjects]
public sealed class KoreanTimeDemoInspector : KoreanTimeSystemInspector
{ protected override string KoreanTitle => "시간 테스트 화면"; }

[CustomEditor(typeof(TimeSkyExposure)), CanEditMultipleObjects]
public sealed class KoreanSkyInspector : KoreanTimeSystemInspector
{ protected override string KoreanTitle => "하늘과 구름"; }

[CustomEditor(typeof(RainWeatherController)), CanEditMultipleObjects]
public sealed class KoreanRainInspector : KoreanTimeSystemInspector
{ protected override string KoreanTitle => "비 날씨"; }

[CustomEditor(typeof(TimeEnvironmentController)), CanEditMultipleObjects]
public sealed class KoreanEnvironmentInspector : KoreanTimeSystemInspector
{ protected override string KoreanTitle => "시간 환경 연결"; }

[CustomEditor(typeof(TimeMovementInputBridge)), CanEditMultipleObjects]
public sealed class KoreanTimeMovementInspector : KoreanTimeSystemInspector
{ protected override string KoreanTitle => "시간 화면 입력 연결"; }
