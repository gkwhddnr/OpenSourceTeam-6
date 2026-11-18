using System;
using UnityEngine;
using OpenSourceTeam6.Time.Core;
namespace OpenSourceTeam6.Time.Unity
{
    public sealed class BusinessTimeController : MonoBehaviour
    {
        [KoreanInspectorLabel("독립 시계 사용"), Tooltip("기본적으로 기존 UI가 시간을 제어합니다. 단독 시험에서만 사용하세요.")]
        [SerializeField] private bool standaloneClock;
        [KoreanInspectorLabel("하루 길이 (초)"), SerializeField, Min(1)] private float dayDurationSeconds = 240;
        [KoreanInspectorLabel("시작 시각"), SerializeField, Range(0, 23)] private float openingHour = 8;
        [KoreanInspectorLabel("종료 시각"), Tooltip("24시를 넘어 이어지는 시각입니다. 32는 다음 날 오전 8시입니다.")]
        [SerializeField, Range(1, 48)] private float closingHour = 32;
        private bool demoClockEnabled, externalClockOwned;
        private float demoElapsed, demoDurationSeconds = 60;
        public BusinessClock Clock { get; } = new BusinessClock();
        public float GameHour => Mathf.Repeat(Mathf.Lerp(openingHour, closingHour, (float)Clock.Progress), 24);
        public float ElapsedGameHours => (closingHour - openingHour) * (float)Clock.Progress;
        public event Action<int> ClosingRequested;
        private void OnEnable() { Clock.Closing += OnClosing; }
        private void OnDisable() { Clock.Closing -= OnClosing; }
        private void Start() { if (standaloneClock) BeginDay(1); }
        private void Update()
        {
            if (standaloneClock && Clock.Phase == BusinessPhase.Open)
                Clock.Advance(UnityEngine.Time.deltaTime, dayDurationSeconds);
            else if (!standaloneClock && demoClockEnabled && !Clock.IsPaused)
            {
                demoElapsed += UnityEngine.Time.deltaTime;
                if (Clock.Phase == BusinessPhase.Ready) BeginDay(1);
                ApplyExternalProgress(Clock.DayId, Mathf.Min(demoElapsed / demoDurationSeconds, 1));
            }
        }

        /// <summary>Enables the isolated demo's clock without taking ownership from a standalone clock.</summary>
        public bool ConfigureDemoClock(float durationSeconds)
        {
            if (standaloneClock || externalClockOwned) return false;
            demoDurationSeconds = float.IsFinite(durationSeconds) ? Mathf.Max(durationSeconds, 1) : 60;
            demoClockEnabled = true;
            return true;
        }

        public void RestartDemoDay()
        {
            if (!demoClockEnabled || standaloneClock) return;
            Clock.IsPaused = false;
            ApplyExternalProgress(Clock.DayId, 1);
            Clock.ConfirmSettlement(Clock.DayId);
            if (BeginDay(Clock.DayId + 1)) demoElapsed = 0;
        }

        /// <summary>Reserve the clock for one external timer before a day begins.</summary>
        public bool AcquireExternalClock()
        {
            if (externalClockOwned || standaloneClock || (demoClockEnabled && (Clock.Phase == BusinessPhase.Open || Clock.Phase == BusinessPhase.Closing)))
                return false;
            externalClockOwned = true;
            demoClockEnabled = false;
            Clock.IsPaused = false;
            return true;
        }

        public void ReleaseExternalClock() { externalClockOwned = false; }
        public bool BeginDay(int dayId)
        {
            if (!float.IsFinite(dayDurationSeconds) || dayDurationSeconds <= 0 ||
                !float.IsFinite(openingHour) || !float.IsFinite(closingHour) ||
                openingHour < 0 || openingHour >= 24 || closingHour > openingHour + 24 || closingHour <= openingHour)
            { Debug.LogError("Invalid business time configuration.", this); return false; }
            return Clock.BeginDay(dayId);
        }
        public bool ApplyExternalProgress(int dayId, double progress)
        { return !standaloneClock && Clock.SetProgress(dayId, progress); }
        private void OnClosing(int dayId) { ClosingRequested?.Invoke(dayId); }
    }
}
