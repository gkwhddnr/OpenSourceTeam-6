using System;

namespace OpenSourceTeam6.Time.Core
{
    /// <summary>Unit direction in the game's +X east, +Y up, +Z north basis.</summary>
    public readonly struct CelestialDirection
    {
        public readonly float X, Y, Z;

        public CelestialDirection(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public readonly struct SunSample
    {
        public readonly CelestialDirection Direction;
        public readonly float Elevation, NightBlend;

        public SunSample(CelestialDirection direction, float elevation, float nightBlend)
        {
            Direction = direction;
            Elevation = elevation;
            NightBlend = nightBlend;
        }
    }

    /// <summary>
    /// Art-directed daily paths matching the existing August-November presentation.
    /// These samples are not astronomical ephemerides and do not depend on Unity state.
    /// </summary>
    public static class CelestialMath
    {
        public const float MinSunsetSpeedMultiplier = .1f;
        public const float MaxSunsetSpeedMultiplier = 2f;
        private const double SunsetHalfArc = .75;

        public static SunSample SampleSun(float hour, float sunrise, float sunset, float sunsetSpeedMultiplier = 1)
        {
            RequireFinite(hour, nameof(hour));
            RequireFinite(sunrise, nameof(sunrise));
            RequireFinite(sunset, nameof(sunset));
            RequireFinite(sunsetSpeedMultiplier, nameof(sunsetSpeedMultiplier));
            if (sunrise < 0 || sunrise >= 24)
                throw new ArgumentOutOfRangeException(nameof(sunrise));
            if (sunset > 24 || sunset <= sunrise || sunset - sunrise >= 24)
                throw new ArgumentOutOfRangeException(nameof(sunset), "Sunrise and sunset must leave both a day and a night interval.");
            if (sunsetSpeedMultiplier < MinSunsetSpeedMultiplier || sunsetSpeedMultiplier > MaxSunsetSpeedMultiplier)
                throw new ArgumentOutOfRangeException(nameof(sunsetSpeedMultiplier));

            double wrappedHour = WrapHour(hour);
            double daytimeHours = (double)sunset - sunrise;
            bool isDaytime = wrappedHour >= sunrise && wrappedHour <= sunset;
            double angle = isDaytime
                ? (wrappedHour - sunrise) / daytimeHours * Math.PI
                : Math.PI + ((wrappedHour < sunrise ? wrappedHour + 24 : wrappedHour) - sunset)
                    / (24 - daytimeHours) * Math.PI;
            double offset = angle - Math.PI;
            double weight = 1 - Math.Abs(offset) / SunsetHalfArc;
            if (weight > 0)
                // Preserve the horizon crossing and blend back to the original speed at the arc boundary.
                angle += (sunsetSpeedMultiplier - 1) * offset * weight * weight;
            CelestialDirection direction = Direction(angle, .15);
            double nightFraction = Clamp01((direction.Y + .2) / .3);
            double night = 1 - nightFraction * nightFraction * (3 - 2 * nightFraction);
            float elevation = isDaytime ? (float)Math.Max(0, Math.Sin(angle)) : 0;
            return new SunSample(direction, elevation, (float)night);
        }

        public static CelestialDirection SampleMoon(float hour, float riseHour, float visibleHours)
        {
            RequireFinite(hour, nameof(hour));
            RequireFinite(riseHour, nameof(riseHour));
            RequireFinite(visibleHours, nameof(visibleHours));
            if (riseHour < 0 || riseHour > 24)
                throw new ArgumentOutOfRangeException(nameof(riseHour));
            if (visibleHours <= 0 || visibleHours >= 24)
                throw new ArgumentOutOfRangeException(nameof(visibleHours));

            double sinceRise = WrapHour((double)hour - riseHour);
            double angle = sinceRise <= visibleHours ? sinceRise / visibleHours * Math.PI
                : Math.PI + (sinceRise - visibleHours) / (24 - visibleHours) * Math.PI;
            return Direction(angle, -.25);
        }

        private static CelestialDirection Direction(double angle, double north)
        {
            double east = Math.Cos(angle), up = Math.Sin(angle);
            double inverseLength = 1 / Math.Sqrt(east * east + up * up + north * north);
            return new CelestialDirection((float)(east * inverseLength), (float)(up * inverseLength),
                (float)(north * inverseLength));
        }

        private static double WrapHour(double hour)
        {
            double wrapped = hour % 24;
            return wrapped < 0 ? wrapped + 24 : wrapped;
        }

        private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

        private static void RequireFinite(float value, string parameter)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameter, "Celestial inputs must be finite.");
        }
    }
}
