using System.Collections;
using NUnit.Framework;
using OpenSourceTeam6.Time.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenSourceTeam6.Time.Tests
{
    public sealed class DemoClockTests
    {
        [UnityTest]
        public IEnumerator DemoClockAdvancesWithoutPanelAndRespectsPauseAndRestart()
        {
            var root = new GameObject("DemoClockTest");
            try
            {
                var clock = root.AddComponent<BusinessTimeController>();
                Assert.That(clock.ConfigureDemoClock(1), Is.True);
                yield return new WaitForSeconds(.1f);
                Assert.That(clock.Clock.Progress, Is.GreaterThan(0));
                clock.Clock.IsPaused = true;
                double paused = clock.Clock.Progress;
                yield return new WaitForSeconds(.1f);
                Assert.That(clock.Clock.Progress, Is.EqualTo(paused));
                int firstDay = clock.Clock.DayId;
                clock.RestartDemoDay();
                Assert.That(clock.Clock.DayId, Is.EqualTo(firstDay + 1));
                Assert.That(clock.Clock.Progress, Is.Zero);
                Assert.That(clock.Clock.IsPaused, Is.False);
            }
            finally { Object.Destroy(root); }
        }
    }
}
