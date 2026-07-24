using System;
using Content.Shared.Imperial.DeimonFly.DeathNote.Scheduling;
using NUnit.Framework;

namespace Content.Tests.Shared.Imperial.DeimonFly.DeathNote;

[TestFixture]
[TestOf(typeof(DeathNoteScheduleMath))]
public sealed class DeathNoteScheduleMathTests
{
    [Test]
    public void TwoMinuteGuidanceForThirtyMinuteDeathStartsAtTwentyEightMinutes()
    {
        var execution = DeathNoteScheduleMath.GetEffectiveExecutionDelay(
            TimeSpan.FromMinutes(30),
            TimeSpan.Zero);
        var prelude = DeathNoteScheduleMath.GetPreludeDelay(
            execution,
            TimeSpan.FromMinutes(2));

        Assert.That(prelude, Is.EqualTo(TimeSpan.FromMinutes(28)));
    }

    [TestCase(120, 0)]
    [TestCase(90, 0)]
    [TestCase(121, 1)]
    public void GuidanceStartsImmediatelyOnlyWhenLessThanTwoMinutesRemain(
        int secondsUntilDeath,
        int expectedPreludeDelay)
    {
        var prelude = DeathNoteScheduleMath.GetPreludeDelay(
            TimeSpan.FromSeconds(secondsUntilDeath),
            TimeSpan.FromMinutes(2));

        Assert.That(prelude, Is.EqualTo(TimeSpan.FromSeconds(expectedPreludeDelay)));
    }

    [Test]
    public void ExecutionAdvanceDoesNotMoveDelayBelowZero()
    {
        var execution = DeathNoteScheduleMath.GetEffectiveExecutionDelay(
            TimeSpan.FromSeconds(0.4),
            TimeSpan.FromSeconds(0.6));

        Assert.That(execution, Is.EqualTo(TimeSpan.Zero));
    }
}
