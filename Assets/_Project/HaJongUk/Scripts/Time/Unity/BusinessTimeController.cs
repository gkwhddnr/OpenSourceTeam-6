using System;
using UnityEngine;
using OpenSourceTeam6.Time.Core;
namespace OpenSourceTeam6.Time.Unity
{
    public sealed class BusinessTimeController : MonoBehaviour
    {
        [Tooltip("Default: existing UI owns time. Enable only in an isolated demo.")]
        [SerializeField] private bool standaloneClock;
        [SerializeField, Min(1)] private float dayDurationSeconds = 240;
        [SerializeField, Range(0, 23)] private float openingHour = 8;
        [Tooltip("Unwrapped end hour; 32 means next morning at 08:00.")]
        [SerializeField, Range(1, 48)] private float closingHour = 32;
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
        }
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
