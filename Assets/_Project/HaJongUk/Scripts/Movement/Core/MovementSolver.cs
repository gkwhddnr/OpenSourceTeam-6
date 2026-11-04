using System;

namespace OpenSourceTeam6.Movement.Core
{
    public static class MovementSolver
    {
        public static WorldPoint HorizontalVelocity(float x, float z, float yawDegrees, float speed)
        {
            if (!WorldPoint.Finite(x) || !WorldPoint.Finite(z) ||
                !WorldPoint.Finite(yawDegrees) || !WorldPoint.Finite(speed) || speed < 0)
                throw new ArgumentOutOfRangeException(nameof(speed), "Invalid movement input/settings.");

            double length = Math.Sqrt((double)x * x + (double)z * z);
            if (length > 1d) { x = (float)(x / length); z = (float)(z / length); }
            double radians = yawDegrees % 360d * Math.PI / 180d;
            return new WorldPoint((float)((x * Math.Cos(radians) + z * Math.Sin(radians)) * speed),
                0, (float)((z * Math.Cos(radians) - x * Math.Sin(radians)) * speed));
        }

        public static float StepSeconds(float seconds)
        {
            if (!WorldPoint.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            return Math.Min(seconds, 0.05f);
        }

        public static float VerticalVelocity(float current, bool grounded, float gravity, float seconds)
        {
            if (!WorldPoint.Finite(current) || !WorldPoint.Finite(gravity) || gravity >= 0)
                throw new ArgumentOutOfRangeException(nameof(gravity));
            float dt = StepSeconds(seconds);
            if (dt == 0) return current;
            if (grounded && current < 0) current = -2f;
            return Math.Max(-50f, current + gravity * dt);
        }
    }
}
