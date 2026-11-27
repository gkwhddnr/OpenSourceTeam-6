using System;
using NUnit.Framework;
using OpenSourceTeam6.Time.Core;

namespace OpenSourceTeam6.Time.Tests
{
    public sealed class CelestialMathTests
    {
        private const float Tolerance = .000002f;

        [TestCase(6, .98893635f, 0, 0)]
        [TestCase(12.25f, 0, .98893635f, 1)]
        [TestCase(18.5f, -.98893635f, 0, 0)]
        [TestCase(.25f, 0, -.98893635f, 0)]
        public void SunRetainsExistingSeptemberPath(float hour, float east, float up, float elevation)
        {
            SunSample sample = CelestialMath.SampleSun(hour, 6, 18.5f);
            AssertDirection(sample.Direction, east, up, .14834045f);
            Assert.That(sample.Elevation, Is.EqualTo(elevation).Within(Tolerance));
        }

        [Test]
        public void NightBlendRetainsSmoothHorizonTransition()
        {
            Assert.That(CelestialMath.SampleSun(12.25f, 6, 18.5f).NightBlend, Is.Zero);
            Assert.That(CelestialMath.SampleSun(.25f, 6, 18.5f).NightBlend, Is.EqualTo(1));
            Assert.That(CelestialMath.SampleSun(18.5f, 6, 18.5f).NightBlend,
                Is.EqualTo(7f / 27).Within(Tolerance));
            float before = CelestialMath.SampleSun(18.4f, 6, 18.5f).NightBlend;
            float after = CelestialMath.SampleSun(18.6f, 6, 18.5f).NightBlend;
            Assert.That(before, Is.LessThan(after));
        }

        [Test]
        public void SunsetSpeedExtendsTwilightWithoutChangingHorizonCrossing()
        {
            const float sunrise = 6;
            const float sunset = 18.5f;
            const float slowerSpeed = .25f;
            var slowBefore = CelestialMath.SampleSun(sunset - .25f, sunrise, sunset, slowerSpeed);
            var slowAfter = CelestialMath.SampleSun(sunset + .25f, sunrise, sunset, slowerSpeed);
            var normalBefore = CelestialMath.SampleSun(sunset - .25f, sunrise, sunset);
            var normalAfter = CelestialMath.SampleSun(sunset + .25f, sunrise, sunset);
            Assert.That(slowBefore.Direction.Y, Is.InRange(0, normalBefore.Direction.Y));
            Assert.That(slowAfter.Direction.Y, Is.InRange(normalAfter.Direction.Y, 0));
            Assert.That(slowBefore.NightBlend, Is.GreaterThan(normalBefore.NightBlend));
            Assert.That(slowAfter.NightBlend, Is.LessThan(normalAfter.NightBlend));
            AssertDirection(CelestialMath.SampleSun(sunset, sunrise, sunset, slowerSpeed).Direction,
                -.98893635f, 0, .14834045f);
            AssertDirection(CelestialMath.SampleSun(sunrise, sunrise, sunset, slowerSpeed).Direction,
                .98893635f, 0, .14834045f);
            Assert.That(CelestialMath.SampleSun(12.25f, sunrise, sunset, slowerSpeed).Elevation, Is.EqualTo(1));
        }

        [TestCase(17, .9701425f, 0)]
        [TestCase(23, 0, .9701425f)]
        [TestCase(5, -.9701425f, 0)]
        [TestCase(11, 0, -.9701425f)]
        public void MoonRetainsIndependentRiseAndNightPath(float hour, float east, float up)
        {
            AssertDirection(CelestialMath.SampleMoon(hour, 17, 12), east, up, -.24253563f);
        }

        [TestCase(-.5f)]
        [TestCase(0)]
        [TestCase(8)]
        [TestCase(23.5f)]
        public void WholeDayOffsetsDoNotChangePresentation(float hour)
        {
            SunSample a = CelestialMath.SampleSun(hour, 6, 18.5f);
            SunSample b = CelestialMath.SampleSun(hour + 48, 6, 18.5f);
            AssertDirection(b.Direction, a.Direction.X, a.Direction.Y, a.Direction.Z);
            Assert.That(b.NightBlend, Is.EqualTo(a.NightBlend).Within(Tolerance));
            Assert.That(b.Elevation, Is.EqualTo(a.Elevation).Within(Tolerance));
            CelestialDirection moon = CelestialMath.SampleMoon(hour, 17, 12);
            AssertDirection(CelestialMath.SampleMoon(hour + 48, 17, 12), moon.X, moon.Y, moon.Z);
        }

        [Test]
        public void MidnightIsContinuousAndMoonRiseAtTwentyFourMeansZero()
        {
            SunSample before = CelestialMath.SampleSun(23.999f, 6, 18.5f);
            SunSample after = CelestialMath.SampleSun(.001f, 6, 18.5f);
            Assert.That(Math.Abs(before.Direction.X - after.Direction.X), Is.LessThan(.001));
            Assert.That(Math.Abs(before.Direction.Y - after.Direction.Y), Is.LessThan(.001));
            CelestialDirection atZero = CelestialMath.SampleMoon(3, 0, 12);
            AssertDirection(CelestialMath.SampleMoon(3, 24, 12), atZero.X, atZero.Y, atZero.Z);
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(float.NaN, 6, 18));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, float.PositiveInfinity, 18));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, 6, float.NegativeInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, -1, 18));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, 6, 25));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, 18, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, 6, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, 0, 24));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, 6, 18, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, 6, 18, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleSun(8, 6, 18, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleMoon(float.PositiveInfinity, 17, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleMoon(8, float.NaN, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleMoon(8, 17, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleMoon(8, -1, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleMoon(8, 25, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleMoon(8, 17, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CelestialMath.SampleMoon(8, 17, 24));
        }

        private static void AssertDirection(CelestialDirection direction, float east, float up, float north)
        {
            Assert.That(direction.X, Is.EqualTo(east).Within(Tolerance));
            Assert.That(direction.Y, Is.EqualTo(up).Within(Tolerance));
            Assert.That(direction.Z, Is.EqualTo(north).Within(Tolerance));
            double lengthSquared = direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z;
            Assert.That(lengthSquared, Is.EqualTo(1).Within(Tolerance));
        }
    }
}
