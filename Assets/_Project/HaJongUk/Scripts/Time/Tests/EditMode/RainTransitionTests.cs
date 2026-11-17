using System;
using NUnit.Framework;
using OpenSourceTeam6.Time.Core;

namespace OpenSourceTeam6.Time.Tests
{
    public sealed class RainTransitionTests
    {
        [Test]
        public void CloudsCompleteBeforeRainAndRemainUntilRainClears()
        {
            var rain = new RainTransition();
            rain.Advance(true, 3, 12, 10);
            Assert.That(rain.CloudIntensity, Is.InRange(.49f, .51f));
            Assert.That(rain.RainIntensity, Is.Zero);
            rain.Advance(true, 3, 12, 10);
            Assert.That(rain.CloudIntensity, Is.EqualTo(1));
            Assert.That(rain.RainIntensity, Is.Zero);
            rain.Advance(true, 3, 12, 10);
            Assert.That(rain.RainIntensity, Is.InRange(.49f, .51f));
            rain.Advance(false, 1, 12, 10);
            Assert.That(rain.CloudIntensity, Is.EqualTo(1));
            Assert.That(rain.RainIntensity, Is.GreaterThan(0));
        }
        [Test]
        public void RainStartsGentlyAndReachesHeavyRain()
        {
            var rain = new RainTransition();
            rain.Advance(true, 1, 12, 10);
            Assert.That(rain.Intensity, Is.InRange(.001f, .03f));
            rain.Advance(true, 5, 12, 10);
            Assert.That(rain.Intensity, Is.EqualTo(.5f).Within(.00001f));
            rain.Advance(true, 6, 12, 10);
            Assert.That(rain.Intensity, Is.EqualTo(1));
        }

        [Test]
        public void ClearingIsGradualAndFinishesExactly()
        {
            var rain = new RainTransition();
            rain.Advance(true, 12, 12, 10);
            rain.Advance(false, 1, 12, 10);
            Assert.That(rain.Intensity, Is.InRange(.95f, .99f));
            rain.Advance(false, 4, 12, 10);
            Assert.That(rain.Intensity, Is.EqualTo(.5f).Within(.00001f));
            rain.Advance(false, 6, 12, 10);
            Assert.That(rain.Intensity, Is.Zero);
        }

        [Test]
        public void RapidReversalContinuesFromCurrentValue()
        {
            var rain = new RainTransition();
            rain.Advance(true, 6, 12, 10);
            rain.Advance(false, 0, 12, 10);
            Assert.That(rain.Intensity, Is.EqualTo(.5f));
            rain.Advance(false, 1, 12, 10);
            Assert.That(rain.Intensity, Is.LessThan(.5f).And.GreaterThan(0));
            rain.Advance(true, 1.2, 12, 10);
            Assert.That(rain.Intensity, Is.EqualTo(.5f).Within(.00001f));
        }

        [Test]
        public void PausedDeltaDoesNotChangeWeather()
        {
            var rain = new RainTransition();
            rain.Advance(true, 4, 12, 10);
            var before = rain.Intensity;
            rain.Advance(false, 0, 12, 10);
            Assert.That(rain.Intensity, Is.EqualTo(before));
        }

        [Test]
        public void FrameSubdivisionPreservesIntensityAndClampsEndpoints()
        {
            var a = new RainTransition();
            var b = new RainTransition();
            a.Advance(true, 9, 12, 10);
            for (int i = 0; i < 540; i++) b.Advance(true, 1.0 / 60, 12, 10);
            Assert.That(a.Intensity, Is.EqualTo(b.Intensity).Within(.00001f));
            b.Advance(true, 1000, 12, 10);
            Assert.That(b.Intensity, Is.EqualTo(1));
            b.Advance(false, 1000, 12, 10);
            Assert.That(b.Intensity, Is.Zero);
        }

        [Test]
        public void InvalidNumbersAreRejectedWithoutChangingState()
        {
            var rain = new RainTransition();
            Assert.Throws<ArgumentOutOfRangeException>(() => rain.Advance(true, double.NaN, 12, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => rain.Advance(true, -1, 12, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => rain.Advance(true, 1, 0, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => rain.Advance(false, 1, 12, double.PositiveInfinity));
            Assert.That(rain.Intensity, Is.Zero);
        }
    }
}
