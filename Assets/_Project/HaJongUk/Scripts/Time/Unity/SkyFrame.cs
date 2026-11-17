using UnityEngine;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Immutable presentation values, with no component or service access.</summary>
    public readonly struct SkyFrame
    {
        public readonly Vector3 SunDirection, MoonDirection;
        public readonly float NightBlend, MoonRadius, CloudIntensity, ElapsedGameHours;

        public SkyFrame(Vector3 sun, Vector3 moon, float night, float moonRadius, float clouds, float elapsed)
        {
            SunDirection = sun; MoonDirection = moon; NightBlend = night;
            MoonRadius = moonRadius; CloudIntensity = clouds; ElapsedGameHours = elapsed;
        }
    }
}
