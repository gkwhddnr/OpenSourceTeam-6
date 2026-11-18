using NUnit.Framework;
using OpenSourceTeam6.Time.Core;

namespace OpenSourceTeam6.Time.Tests
{
    public sealed class BusinessClockLifecycleTests
    {
        [Test]
        public void ClosingAndSettlementAreAppliedOncePerDay()
        {
            var clock = new BusinessClock();
            int closures = 0;
            clock.Closing += id => closures++;
            Assert.That(clock.BeginDay(1), Is.True);
            Assert.That(clock.SetProgress(1, .5), Is.True);
            Assert.That(clock.SetProgress(1, 1), Is.True);
            Assert.That(clock.SetProgress(1, 1), Is.False);
            Assert.That(clock.ConfirmSettlement(1), Is.True);
            Assert.That(clock.ConfirmSettlement(1), Is.False);
            Assert.That(closures, Is.EqualTo(1));
            Assert.That(clock.BeginDay(2), Is.True);
            Assert.That(clock.SetProgress(1, .9), Is.False);
        }

        [Test]
        public void ReturningToLobbyCancelsOnlyCurrentDayWithoutClosingEvent()
        {
            var clock = new BusinessClock();
            int closures = 0;
            clock.Closing += id => closures++;
            clock.BeginDay(1);
            Assert.That(clock.CancelDay(2), Is.False);
            Assert.That(clock.CancelDay(1), Is.True);
            Assert.That(clock.CancelDay(1), Is.False);
            Assert.That(closures, Is.Zero);
            Assert.That(clock.BeginDay(2), Is.True);
            Assert.That(clock.Progress, Is.Zero);
        }
    }
}
