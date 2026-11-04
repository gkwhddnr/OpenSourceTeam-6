using System;

namespace OpenSourceTeam6.Camera.Core
{
    public readonly struct LookState
    {
        public float YawDegrees { get; }
        public float PitchDegrees { get; }

        public LookState(float yawDegrees, float pitchDegrees)
        {
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
        }
    }

    public static class LookSolver
    {
        // Mouse delta is displacement, not angular velocity; do not multiply by dt.
        public static LookState Step(LookState state, float deltaX, float deltaY,
            float degreesPerUnit, float minPitch = -80f, float maxPitch = 80f)
        {
            if (!Finite(state.YawDegrees) || !Finite(state.PitchDegrees) ||
                !Finite(deltaX) || !Finite(deltaY) || !Finite(degreesPerUnit) ||
                degreesPerUnit < 0 || !Finite(minPitch) || !Finite(maxPitch) ||
                minPitch > maxPitch || minPitch < -89f || maxPitch > 89f)
                throw new ArgumentOutOfRangeException(nameof(degreesPerUnit), "Invalid look input/settings.");

            double yaw = ((double)state.YawDegrees + (double)deltaX * degreesPerUnit) % 360d;
            if (yaw < 0d) yaw += 360d;
            double pitch = state.PitchDegrees - (double)deltaY * degreesPerUnit;
            return new LookState((float)yaw, (float)Math.Max(minPitch, Math.Min(maxPitch, pitch)));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
