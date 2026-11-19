using System;
namespace OpenSourceTeam6.Time.Core
{
    public enum BusinessPhase { Ready, Open, Closing, Settled }
    public sealed class BusinessClock
    {
        public int DayId { get; private set; }
        public BusinessPhase Phase { get; private set; }
        public double Progress { get; private set; }
        public bool IsPaused { get; set; }
        public event Action Changed;
        public event Action<int> Closing;
        public bool BeginDay(int dayId)
        {
            if (dayId <= DayId || (Phase != BusinessPhase.Ready && Phase != BusinessPhase.Settled)) return false;
            DayId = dayId; Progress = 0; IsPaused = false; Phase = BusinessPhase.Open;
            Changed?.Invoke(); return true;
        }
        public void Advance(double seconds, double duration)
        {
            if (!Finite(seconds) || seconds < 0 || !Finite(duration) || duration <= 0) throw new ArgumentOutOfRangeException();
            if (!IsPaused) SetProgress(DayId, Progress + seconds / duration);
        }
        public bool SetProgress(int dayId, double progress)
        {
            if (!Finite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
            if (dayId != DayId || Phase != BusinessPhase.Open || IsPaused || progress < Progress) return false;
            Progress = Math.Min(1, Math.Max(0, progress));
            bool closing = Progress >= 1;
            if (closing) Phase = BusinessPhase.Closing;
            Changed?.Invoke();
            if (closing) Closing?.Invoke(DayId);
            return true;
        }
        public bool ConfirmSettlement(int dayId)
        {
            if (dayId != DayId || Phase != BusinessPhase.Closing) return false;
            Phase = BusinessPhase.Settled; Changed?.Invoke(); return true;
        }
        public bool CancelDay(int dayId)
        {
            if (dayId != DayId || (Phase != BusinessPhase.Open && Phase != BusinessPhase.Closing)) return false;
            Phase = BusinessPhase.Settled; Changed?.Invoke(); return true;
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
