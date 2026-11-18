using System;

namespace OpenSourceTeam6.Time.Core
{
    /// <summary>One continuous weather envelope, in scaled real seconds, independent of day length.</summary>
    public sealed class RainTransition
    {
        private double progress;
        public float Intensity => (float)(progress * progress * (3 - 2 * progress));
        // Clouds complete during the first half; rainfall occupies the second half.
        public float CloudIntensity
        {
            get { double t = Math.Min(1, progress * 2); return (float)(t * t * (3 - 2 * t)); }
        }
        public float RainIntensity
        {
            get { double t = Math.Max(0, progress * 2 - 1); return (float)(t * t * (3 - 2 * t)); }
        }

        public void Advance(bool isRain, double seconds, double rainInSeconds, double rainOutSeconds)
        {
            if (!Finite(seconds) || seconds < 0 || !Finite(rainInSeconds) || rainInSeconds <= 0 ||
                !Finite(rainOutSeconds) || rainOutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Weather durations must be finite and positive.");
            // Reverse from the existing envelope; toggling never restarts or jumps the intensity.
            progress = isRain ? Math.Min(1, progress + seconds / rainInSeconds) :
                Math.Max(0, progress - seconds / rainOutSeconds);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
