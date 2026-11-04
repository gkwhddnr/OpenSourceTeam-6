using System;
using NUnit.Framework;
using OpenSourceTeam6.Camera.Core;
using OpenSourceTeam6.Movement.Core;

namespace OpenSourceTeam6.Movement.Tests
{
    public sealed class MovementRulesTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void WalkingTenSecondsCoversThirtyMetres(int fps)
        {
            var velocity = MovementSolver.HorizontalVelocity(0, 1, 0, 3);
            float distance = 0;
            for (int i = 0; i < fps * 10; i++) distance += velocity.Z * MovementSolver.StepSeconds(1f / fps);
            Assert.That(distance, Is.EqualTo(30f).Within(0.01f));
        }

        [Test]
        public void DiagonalCannotMoveFasterAndYawRotatesMovement()
        {
            var diagonal = MovementSolver.HorizontalVelocity(1, 1, 0, 3);
            Assert.That(Math.Sqrt(diagonal.X * diagonal.X + diagonal.Z * diagonal.Z), Is.EqualTo(3).Within(0.001));
            var right = MovementSolver.HorizontalVelocity(0, 1, 90, 3);
            Assert.That(right.X, Is.EqualTo(3).Within(0.001));
            Assert.That(right.Z, Is.EqualTo(0).Within(0.001));
        }

        [Test]
        public void LookClampsPitchWrapsYawAndRejectsInvalidInput()
        {
            var look = LookSolver.Step(new LookState(359, 0), 2, -1000, 1);
            Assert.That(look.YawDegrees, Is.EqualTo(1));
            Assert.That(look.PitchDegrees, Is.EqualTo(80));
            Assert.Throws<ArgumentOutOfRangeException>(() => LookSolver.Step(look, float.NaN, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MovementSolver.HorizontalVelocity(float.PositiveInfinity, 0, 0, 3));
        }

        [Test]
        public void PauseDoesNotIntegrateGravityAndHitchesAreBounded()
        {
            Assert.That(MovementSolver.StepSeconds(0), Is.Zero);
            Assert.That(MovementSolver.StepSeconds(2), Is.EqualTo(0.05f));
            Assert.That(MovementSolver.VerticalVelocity(-5, false, -20, 0), Is.EqualTo(-5));
        }

        [Test]
        public void IndependentLocksAndDoubleDisposeDoNotUnlockAnotherOwner()
        {
            var set = new InputLockSet();
            var a = set.AcquireLock("Dialogue"); var b = set.AcquireLock("Menu");
            a.Dispose(); a.Dispose();
            Assert.That(set.IsLocked, Is.True);
            b.Dispose(); Assert.That(set.IsLocked, Is.False);
        }

        [Test]
        public void ReplacementAndCancellationProduceExactlyOneOutcomePerRequest()
        {
            var tracker = new MoveRequestTracker();
            var a = tracker.Begin(default); var b = tracker.Begin(default);
            Assert.That(b.RequestId, Is.GreaterThan(a.RequestId));
            Assert.That(tracker.Cancel(a.RequestId, default), Is.False);
            Assert.That(tracker.Cancel(b.RequestId, default), Is.True);
            Assert.That(tracker.Cancel(b.RequestId, default), Is.False);
            Assert.That(tracker.TryDequeue(out var first), Is.True);
            Assert.That(first.RequestId, Is.EqualTo(a.RequestId));
            Assert.That(first.Cancellation, Is.EqualTo(CancelReason.Replaced));
            Assert.That(tracker.TryDequeue(out var second), Is.True);
            Assert.That(second.RequestId, Is.EqualTo(b.RequestId));
            Assert.That(second.Cancellation, Is.EqualTo(CancelReason.Requested));
            Assert.That(tracker.TryDequeue(out _), Is.False);
        }

        [Test]
        public void FullQueueRejectsNewRequestButKeepsTerminalSlotForActiveRequest()
        {
            var tracker = new MoveRequestTracker();
            for (int i = 0; i < MoveRequestTracker.Capacity; i++) Assert.That(tracker.Begin(default).Accepted, Is.True);
            long active = tracker.ActiveId;
            Assert.That(tracker.Begin(default).Accepted, Is.False);
            Assert.That(tracker.ActiveId, Is.EqualTo(active));
            tracker.Cancel(active, default);
            Assert.That(tracker.PendingCount, Is.EqualTo(MoveRequestTracker.Capacity));
            while (tracker.TryDequeue(out _)) { }
            Assert.That(tracker.Begin(default).Accepted, Is.True);
        }
    }
}
