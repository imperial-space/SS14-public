using System;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Parsing;
using NUnit.Framework;

namespace Content.Tests.Shared.Imperial.DeimonFly.DeathNote;

[TestFixture]
[TestOf(typeof(DeathNoteInputParser))]
public sealed class DeathNoteInputParserTests
{
    [TestCase("00:00:00", 0, 0, 0)]
    [TestCase("01:02:03", 1, 2, 3)]
    [TestCase("23:59:59", 23, 59, 59)]
    [TestCase(" 01:02:03 ", 1, 2, 3)]
    public void StrictRoundTimeAcceptsValidValues(string input, int hours, int minutes, int seconds)
    {
        Assert.That(DeathNoteInputParser.TryParseRoundTime(input, out var result), Is.True);
        Assert.That(result, Is.EqualTo(new TimeSpan(hours, minutes, seconds)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("1:02:03")]
    [TestCase("01:2:03")]
    [TestCase("01:02:3")]
    [TestCase("24:00:00")]
    [TestCase("00:60:00")]
    [TestCase("00:00:60")]
    [TestCase("-01:00:00")]
    [TestCase("01:02:03.5")]
    [TestCase("01-02-03")]
    public void StrictRoundTimeRejectsInvalidValues(string input)
    {
        Assert.That(DeathNoteInputParser.TryParseRoundTime(input, out _), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void EmptyCauseSelectsStandardEntry(string input)
    {
        Assert.That(DeathNoteInputParser.TryParseCause(input, out var result), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.EntryType, Is.EqualTo(DeathNoteEntryType.Standard));
            Assert.That(result.CauseText, Is.Empty);
            Assert.That(result.CustomText, Is.Null);
        });
    }

    [Test]
    public void NonEmptyCauseBecomesPresetCandidateWithoutChangingInnerWhitespace()
    {
        Assert.That(DeathNoteInputParser.TryParseCause("  сердечный  приступ  ", out var result), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.EntryType, Is.EqualTo(DeathNoteEntryType.Preset));
            Assert.That(result.CauseText, Is.EqualTo("сердечный  приступ"));
            Assert.That(result.CustomText, Is.Null);
        });
    }

    [TestCase("  ! Упал с лестницы  ", "Упал с лестницы")]
    [TestCase("!!foo", "!foo")]
    public void LeadingBangCreatesCustomScenario(string input, string expectedScenario)
    {
        Assert.That(DeathNoteInputParser.TryParseCause(input, out var result), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.EntryType, Is.EqualTo(DeathNoteEntryType.Custom));
            Assert.That(result.CustomText, Is.EqualTo(expectedScenario));
        });
    }

    [Test]
    public void CustomScenarioPreservesYoAndOriginalLetterCase()
    {
        const string scenario = "Ёлкин Ёжик в 00:21:40 умрёт, сохранив своё имя.";

        Assert.That(DeathNoteInputParser.TryParseCause($"! {scenario}", out var result), Is.True);
        Assert.That(result.CustomText, Is.EqualTo(scenario));
    }

    [TestCase("!")]
    [TestCase("!   ")]
    public void EmptyCustomScenarioIsRejected(string input)
    {
        Assert.That(DeathNoteInputParser.TryParseCause(input, out _), Is.False);
    }

    [Test]
    public void BangInsideCauseDoesNotCreateCustomScenario()
    {
        Assert.That(DeathNoteInputParser.TryParseCause("foo ! bar", out var result), Is.True);
        Assert.That(result.EntryType, Is.EqualTo(DeathNoteEntryType.Preset));
    }

    [TestCase(
        "Лукоса Икс-Нотата в 00:23:21 умрёт от сердечного приступа.",
        "Лукоса Икс-Нотата",
        "00:23:21",
        "сердечного приступа")]
    [TestCase(
        "Luke X-Notata at 00:23:21 shall die from a heart attack.",
        "Luke X-Notata",
        "00:23:21",
        "a heart attack")]
    public void RoleplayPresetLineIsParsed(
        string input,
        string expectedName,
        string expectedTime,
        string expectedCause)
    {
        Assert.That(DeathNoteInputParser.TryParseLine(input, out var line), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(line.TargetName, Is.EqualTo(expectedName));
            Assert.That(line.ExecutionTime, Is.EqualTo(expectedTime));
            Assert.That(line.CauseText, Is.EqualTo(expectedCause));
        });
    }

    [TestCase("Бэлла Ковальчук в 00:30:00 исполнит: покинет мостик.", "покинет мостик")]
    [TestCase("Bella Kovalchuk at 00:30:00 shall fulfill: leave the bridge.", "leave the bridge")]
    public void RoleplayCustomLineIsParsed(string input, string expectedScenario)
    {
        Assert.That(DeathNoteInputParser.TryParseLine(input, out var line), Is.True);
        Assert.That(line.CauseText, Is.EqualTo($"! {expectedScenario}"));
    }

    [TestCase("Линифия Акватика умрёт от падения торгового автомата", "Линифия Акватика", "падения торгового автомата")]
    [TestCase("Линифия Акватика умрет от падения автомата", "Линифия Акватика", "падения автомата")]
    [TestCase("Линифия Акватика погибнет от карпов", "Линифия Акватика", "карпов")]
    [TestCase("Линифия Акватика скончается от яда", "Линифия Акватика", "яда")]
    public void CauseWithoutTimeUsesTheDefaultDelay(string input, string expectedName, string expectedCause)
    {
        Assert.That(DeathNoteInputParser.TryParseLine(input, out var line), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(line.TargetName, Is.EqualTo(expectedName));
            Assert.That(line.ExecutionTime, Is.Empty);
            Assert.That(line.CauseText, Is.EqualTo(expectedCause));
        });
    }

    [TestCase("Линифия Акватика")]
    [TestCase("Линифия Акватика умрёт")]
    [TestCase("Линифия Акватика умрет")]
    [TestCase("Линифия Акватика погибнет")]
    [TestCase("Линифия Акватика скончается")]
    public void BareNameOrNameOnlyDeathUsesHeartAttackAfterDefaultDelay(string input)
    {
        Assert.That(DeathNoteInputParser.TryParseLine(input, out var line), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(line.TargetName, Is.EqualTo("Линифия Акватика"));
            Assert.That(line.ExecutionTime, Is.Empty);
            Assert.That(line.CauseText, Is.Empty);
        });
    }

    [TestCase(
        "Хей-Сакея !в 00:21:40 застрелит себя перед смертью оставив записку \"я иду за вами\".",
        "Хей-Сакея",
        "00:21:40",
        "Хей-Сакея в 00:21:40 застрелит себя перед смертью оставив записку \"я иду за вами\".")]
    [TestCase(
        "Хей-Сакея !застрелит себя перед смертью.",
        "Хей-Сакея",
        "",
        "Хей-Сакея застрелит себя перед смертью.")]
    [TestCase(
        "Хей-Сакея в 00:21:40 !застрелит себя перед смертью.",
        "Хей-Сакея",
        "00:21:40",
        "Хей-Сакея в 00:21:40 застрелит себя перед смертью.")]
    [TestCase(
        "!Хей-Сакея в 00:21:40 застрелит себя перед смертью.",
        "Хей-Сакея",
        "00:21:40",
        "Хей-Сакея в 00:21:40 застрелит себя перед смертью.")]
    public void BangSyntaxPreservesTheEntireVictimInstruction(
        string input,
        string expectedName,
        string expectedTime,
        string expectedVisibleText)
    {
        Assert.That(DeathNoteInputParser.TryParseLine(input, out var line), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(line.TargetName, Is.EqualTo(expectedName));
            Assert.That(line.ExecutionTime, Is.EqualTo(expectedTime));
            Assert.That(line.CauseText, Is.EqualTo($"! {expectedVisibleText}"));
            Assert.That(DeathNoteInputParser.NormalizeWrittenText(input), Is.EqualTo(expectedVisibleText));
        });
    }

    [TestCase("Имя | 00:20:00 | сердечный приступ")]
    [TestCase("!Имя исполнит невозможное")]
    [TestCase("Имя !")]
    public void AmbiguousOrLegacyLineIsRejected(string input)
    {
        Assert.That(DeathNoteInputParser.TryParseLine(input, out _), Is.False);
    }

    [Test]
    public void NameNormalizationOnlyTrimsEdges()
    {
        Assert.That(DeathNoteInputParser.NormalizeName("  Иван  Иванов  "), Is.EqualTo("Иван  Иванов"));
    }

    [TestCase("питьё", "питье")]
    [TestCase("СМЕРТЬ ОТ ЁЛКИ", "СМЕРТЬ ОТ ЕЛКИ")]
    [TestCase("  карпов  ", "карпов")]
    public void CauseNormalizationTreatsYoAsYe(string input, string expected)
    {
        Assert.That(DeathNoteInputParser.NormalizeCause(input), Is.EqualTo(expected));
    }

    [TestCase(
        "  Пимо Райнбови в 00:04:00 умрет от космических карпов.  ",
        "Пимо Райнбови в 00:04:00 умрет от космических карпов.")]
    [TestCase(
        "  Хей-Сакея !в 00:21:40 оставит записку «я иду за вами».  ",
        "Хей-Сакея в 00:21:40 оставит записку «я иду за вами».")]
    [TestCase(
        "  Хей-Сакея !оставит записку «я иду за вами».  ",
        "Хей-Сакея оставит записку «я иду за вами».")]
    [TestCase(
        "  !Хей-Сакея в 00:21:40 оставит записку «я иду за вами».  ",
        "Хей-Сакея в 00:21:40 оставит записку «я иду за вами».")]
    [TestCase("  Линифия Акватика  ", "Линифия Акватика")]
    public void VisibleWrittenTextPreservesTheAuthorsWordsAndHidesOnlySyntax(
        string input,
        string expected)
    {
        Assert.That(DeathNoteInputParser.NormalizeWrittenText(input), Is.EqualTo(expected));
    }

    [Test]
    public void EmptyTimeUsesDefaultDelay()
    {
        var now = TimeSpan.FromMinutes(10);
        var delay = TimeSpan.FromSeconds(40);

        Assert.That(
            DeathNoteInputValidator.TryResolveScheduledTime(now, "  ", delay, out var scheduled, out var failure),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(scheduled, Is.EqualTo(now + delay));
            Assert.That(failure, Is.EqualTo(DeathNoteFailureReason.None));
        });
    }

    [TestCase("00:10:00")]
    [TestCase("00:09:59")]
    public void PastOrEqualTimeIsRejected(string input)
    {
        Assert.That(
            DeathNoteInputValidator.TryResolveScheduledTime(
                TimeSpan.FromMinutes(10),
                input,
                TimeSpan.FromSeconds(40),
                out _,
                out var failure),
            Is.False);
        Assert.That(failure, Is.EqualTo(DeathNoteFailureReason.TimeInPast));
    }

    [Test]
    public void TerminalStatusesCannotTransition()
    {
        var terminalStatuses = new[]
        {
            DeathNoteEntryStatus.CustomDelivered,
            DeathNoteEntryStatus.DeathConfirmed,
            DeathNoteEntryStatus.Failed,
            DeathNoteEntryStatus.TargetMissing,
            DeathNoteEntryStatus.TargetAlreadyDead,
            DeathNoteEntryStatus.AmbiguousName,
            DeathNoteEntryStatus.InvalidFormat,
            DeathNoteEntryStatus.Cancelled,
        };

        foreach (var terminal in terminalStatuses)
        {
            foreach (var destination in Enum.GetValues<DeathNoteEntryStatus>())
            {
                Assert.That(
                    DeathNoteStatusTransitions.CanTransition(terminal, destination),
                    Is.False,
                    $"{terminal} unexpectedly transitioned to {destination}.");
            }
        }
    }

    [Test]
    public void ScheduledEntryUsesExpectedExecutionPath()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                DeathNoteStatusTransitions.CanTransition(
                    DeathNoteEntryStatus.Submitted,
                    DeathNoteEntryStatus.Scheduled),
                Is.True);
            Assert.That(
                DeathNoteStatusTransitions.CanTransition(
                    DeathNoteEntryStatus.Scheduled,
                    DeathNoteEntryStatus.Executing),
                Is.True);
            Assert.That(
                DeathNoteStatusTransitions.CanTransition(
                    DeathNoteEntryStatus.Executing,
                    DeathNoteEntryStatus.EffectStarted),
                Is.True);
            Assert.That(
                DeathNoteStatusTransitions.CanTransition(
                    DeathNoteEntryStatus.EffectStarted,
                    DeathNoteEntryStatus.DeathConfirmed),
                Is.True);
        });
    }
}
