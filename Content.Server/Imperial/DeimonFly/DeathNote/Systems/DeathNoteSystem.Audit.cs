using Content.Server.Administration.Logs;
using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Shared.Database;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Robust.Shared.Player;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteSystem
{
    public void SendAdministrativeInfluence(
        ICommonSession targetSession,
        string scenario,
        string administrator)
    {
        var safeScenario = ClampAudit(scenario.Trim());
        RaiseNetworkEvent(
            new DeathNoteInfluenceMessage(safeScenario, true),
            targetSession);
        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"Death Note administrator {administrator} sent a custom influence to " +
            $"{targetSession.Name} ({targetSession.UserId}): {safeScenario}");
    }

    private void LogConfirmedEntry(DeathNoteEntry entry, DeathNoteFailureReason failureReason)
    {
        var target = entry.TargetEntity is { } targetEntity
            ? Snapshot(targetEntity)
            : "<unresolved>";
        _adminLog.Add(
            LogType.Action,
            LogImpact.Extreme,
            $"{entry.WriterEntity:player} confirmed Death Note entry #{entry.EntryId} on {entry.NotebookEntity:entity}: " +
            $"enteredName='{ClampAudit(entry.EnteredTargetName)}', target='{target}', " +
            $"page={entry.PageIndex + 1}, line={entry.LineIndex + 1}, " +
            $"type={entry.EntryType}, preset='{entry.PresetId?.ToString() ?? string.Empty}', " +
            $"scheduled={FormatRoundTime(entry.ScheduledRoundTime)}, failure={failureReason}, " +
            $"cause='{ClampAudit(entry.CauseText)}'.");
    }

    private void LogExecutionResult(DeathNoteEntry entry, bool success, string details)
    {
        _adminLog.Add(
            LogType.Action,
            success ? LogImpact.Extreme : LogImpact.High,
            $"Death Note entry #{entry.EntryId} execution {(success ? "succeeded" : "failed")}: " +
            $"{ClampAudit(details)}.");
    }

    private string Snapshot(EntityUid? entity)
    {
        if (entity is not { } uid)
            return string.Empty;

        return Deleted(uid) ? $"deleted entity {uid}" : ToPrettyString(uid);
    }

    private static string ClampAudit(string? text)
    {
        var sanitized = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        return sanitized.Length <= MaxAuditFieldLength
            ? sanitized
            : sanitized[..MaxAuditFieldLength] + "…";
    }

    private static string FormatRoundTime(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private static uint NextRevision(uint revision)
    {
        revision++;
        return revision == 0 ? 1 : revision;
    }
}
