using System.Collections.Immutable;
using Content.Server.Administration.Logs;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Administration.Managers;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Models;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// BUI административного журнала только для чтения. Расширенные строки всегда отправляются адресно.
/// </summary>
public sealed class DeathNoteAdminLedgerSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ISharedAdminManager _admins = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly DeathNoteJournalSystem _journal = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathNoteAdminLedgerComponent, BoundUserInterfaceMessageAttempt>(OnUiAttempt);
        Subs.BuiEvents<DeathNoteAdminLedgerComponent>(DeathNoteUiKey.AdminLedger, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<DeathNoteAdminPageRequestMessage>(OnPageRequested);
        });
    }

    private void OnUiAttempt(
        Entity<DeathNoteAdminLedgerComponent> ent,
        ref BoundUserInterfaceMessageAttempt args)
    {
        if (!args.UiKey.Equals(DeathNoteUiKey.AdminLedger))
            return;

        if (CanUseLedger(args.Actor, ent.Owner))
            return;

        args.Cancel();
        var runtime = EnsureComp<DeathNoteAdminLedgerRuntimeComponent>(ent.Owner);
        if (_timing.CurTime < runtime.NextInvalidAttemptLogTime)
            return;

        runtime.NextInvalidAttemptLogTime = _timing.CurTime + ent.Comp.InvalidAttemptLogCooldown;
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{args.Actor:player} attempted to access protected Death Note ledger {ent.Owner:entity}.");
    }

    private void OnOpened(EntityUid uid, DeathNoteAdminLedgerComponent component, BoundUIOpenedEvent args)
    {
        if (!CanUseLedger(args.Actor, uid))
        {
            _ui.CloseUi(uid, DeathNoteUiKey.AdminLedger, args.Actor);
            return;
        }

        SendPage(uid, component, args.Actor, 0);
    }

    private void OnPageRequested(
        EntityUid uid,
        DeathNoteAdminLedgerComponent component,
        DeathNoteAdminPageRequestMessage message)
    {
        if (!_ui.IsUiOpen(uid, DeathNoteUiKey.AdminLedger, message.Actor) ||
            !CanUseLedger(message.Actor, uid))
        {
            _ui.CloseUi(uid, DeathNoteUiKey.AdminLedger, message.Actor);
            return;
        }

        SendPage(uid, component, message.Actor, message.Page);
    }

    private void SendPage(
        EntityUid uid,
        DeathNoteAdminLedgerComponent component,
        EntityUid actor,
        int requestedPage)
    {
        var pageSize = Math.Max(1, component.EntriesPerPage);
        var totalEntries = _journal.EntryCount;
        var pageCount = Math.Max(
            1,
            (int) Math.Min(
                int.MaxValue,
                ((long) totalEntries + pageSize - 1) / pageSize));
        var page = Math.Clamp(requestedPage, 0, pageCount - 1);
        var responseEntries = ImmutableArray.CreateBuilder<DeathNoteAdminEntry>(pageSize);

        foreach (var entry in _journal.GetReversePage(page, pageSize))
        {
            DeathNoteDestructionLevel? destruction = null;
            if (entry.PresetId is { } presetId &&
                _prototypes.TryIndex(presetId, out DeathNotePresetPrototype? preset))
            {
                destruction = preset.Destruction;
            }

            responseEntries.Add(new DeathNoteAdminEntry(
                entry.EntryId,
                entry.PageIndex,
                entry.LineIndex,
                entry.CreatedRoundTime,
                entry.ScheduledRoundTime,
                entry.NotebookSnapshot,
                entry.OwnerSnapshot,
                entry.WriterSnapshot,
                entry.EnteredTargetName,
                entry.CauseText,
                entry.EntryType,
                entry.PresetId?.ToString() ?? string.Empty,
                destruction,
                entry.ResolvedTargetSnapshot,
                entry.Status,
                entry.Status is DeathNoteEntryStatus.EffectStarted or
                    DeathNoteEntryStatus.DeathConfirmed,
                entry.Status == DeathNoteEntryStatus.DeathConfirmed,
                entry.FailureReason,
                entry.Result ?? string.Empty,
                entry.Error ?? string.Empty));
        }

        _ui.ServerSendUiMessage(
            uid,
            DeathNoteUiKey.AdminLedger,
            new DeathNoteAdminPageResponseMessage(
                responseEntries.ToImmutable(),
                page,
                pageCount,
                totalEntries),
            actor);
    }

    private bool CanUseLedger(EntityUid actor, EntityUid ledger)
    {
        if (Deleted(actor) || Deleted(ledger) || !_admins.IsAdmin(actor) ||
            !TryComp(actor, out HandsComponent? hands) ||
            !_hands.IsHolding((actor, hands), ledger))
        {
            return false;
        }

        return _interaction.InRangeUnobstructed(actor, ledger);
    }
}
