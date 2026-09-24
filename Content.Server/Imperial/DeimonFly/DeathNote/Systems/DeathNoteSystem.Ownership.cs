using System.Collections.Immutable;
using Content.Server.Administration.Logs;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteSystem
{
    private void OnNotebookEquipped(Entity<DeathNoteComponent> ent, ref GotEquippedHandEvent args)
    {
        var runtime = EnsureComp<DeathNoteRuntimeComponent>(ent.Owner);

        if (!ent.Comp.AssignsPermanentOwner)
        {
            SetHolderRelation(ent.Owner, runtime, args.User);
            return;
        }

        if (runtime.OwnerEntity == null)
        {
            RemoveHolderRelation(ent.Owner, runtime, log: false);
            runtime.OwnerEntity = args.User;
            AddOwnerNotebook(args.User, ent.Owner, ent.Comp.OwnershipChannel);
            _adminLog.Add(
                LogType.Action,
                LogImpact.Medium,
                $"{args.User:player} became the owner of {ent.Owner:entity} in Death Note channel {ent.Comp.OwnershipChannel} by taking it into a hand.");
            return;
        }

        if (runtime.OwnerEntity == args.User)
        {
            RemoveHolderRelation(ent.Owner, runtime, log: false);
            return;
        }

        SetHolderRelation(ent.Owner, runtime, args.User);
    }

    private void OnNotebookUnequipped(Entity<DeathNoteComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!TryComp(ent.Owner, out DeathNoteRuntimeComponent? runtime) || runtime.CurrentHolder != args.User)
            return;

        RemoveHolderRelation(ent.Owner, runtime);
    }

    private void OnNotebookShutdown(Entity<DeathNoteComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp(ent.Owner, out DeathNoteRuntimeComponent? runtime))
            return;

        RemoveHolderRelation(ent.Owner, runtime, log: false);
        RemoveOwnerRelation(ent.Owner, runtime, log: false);
    }

    private void OnOwnerShutdown(Entity<DeathNoteOwnerComponent> ent, ref ComponentShutdown args)
    {
        ReleaseOwnedNotebooks(ent.Owner, ent.Comp.Notebooks);
    }

    private void OnOwner2Shutdown(Entity<DeathNoteOwner2Component> ent, ref ComponentShutdown args)
    {
        ReleaseOwnedNotebooks(ent.Owner, ent.Comp.Notebooks);
    }

    private void OnOwner3Shutdown(Entity<DeathNoteOwner3Component> ent, ref ComponentShutdown args)
    {
        ReleaseOwnedNotebooks(ent.Owner, ent.Comp.Notebooks);
    }

    private void OnHolderShutdown(Entity<DeathNoteHolderComponent> ent, ref ComponentShutdown args)
    {
        ReleaseHeldNotebooks(ent.Owner, ent.Comp.Notebooks);
    }

    private void OnHolder2Shutdown(Entity<DeathNoteHolder2Component> ent, ref ComponentShutdown args)
    {
        ReleaseHeldNotebooks(ent.Owner, ent.Comp.Notebooks);
    }

    private void OnHolder3Shutdown(Entity<DeathNoteHolder3Component> ent, ref ComponentShutdown args)
    {
        ReleaseHeldNotebooks(ent.Owner, ent.Comp.Notebooks);
    }

    private void ReleaseOwnedNotebooks(EntityUid owner, HashSet<EntityUid> notebooks)
    {
        foreach (var notebook in ImmutableArray.CreateRange(notebooks))
        {
            notebooks.Remove(notebook);
            if (!TryComp(notebook, out DeathNoteRuntimeComponent? runtime) || runtime.OwnerEntity != owner)
                continue;

            runtime.OwnerEntity = null;
            RemoveHolderRelation(notebook, runtime, log: false);
            _adminLog.Add(
                LogType.Action,
                LogImpact.Medium,
                $"{owner:entity} relinquished ownership of {notebook:entity}; the next character to take it into a hand can become its owner.");
        }

        if (!Terminating(owner))
            _eye.RefreshVisibilityMask(owner);
    }

    private void ReleaseHeldNotebooks(EntityUid holder, HashSet<EntityUid> notebooks)
    {
        foreach (var notebook in ImmutableArray.CreateRange(notebooks))
        {
            notebooks.Remove(notebook);
            if (!TryComp(notebook, out DeathNoteRuntimeComponent? runtime) || runtime.CurrentHolder != holder)
                continue;

            runtime.CurrentHolder = null;
            RemoveHolderNotebooks(holder, notebook);
        }

        if (!Terminating(holder))
            _eye.RefreshVisibilityMask(holder);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        ResetRuntimeState();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        ResetRuntimeState();
    }

    private void ResetRuntimeState()
    {
        var query = EntityQueryEnumerator<DeathNoteRuntimeComponent>();
        while (query.MoveNext(out var notebook, out var runtime))
        {
            RemoveHolderRelation(notebook, runtime, log: false);
            RemoveOwnerRelation(notebook, runtime, log: false);
            runtime.EntryIds.Clear();
            runtime.NextSubmissionTime = TimeSpan.Zero;
            runtime.NextInvalidAttemptLogTime = TimeSpan.Zero;
            // Старая открытая форма не должна стать валидной после очистки раунда.
            runtime.SubmissionRevision = NextRevision(runtime.SubmissionRevision);
        }

        var trackedTargets = EntityQueryEnumerator<DeathNoteTargetTrackingComponent>();
        while (trackedTargets.MoveNext(out var target, out var tracker))
        {
            RemoveOwnedUnrevivable(target, tracker);
            RemCompDeferred<DeathNoteTargetTrackingComponent>(target);
        }
    }

    private void SetHolderRelation(EntityUid notebook, DeathNoteRuntimeComponent runtime, EntityUid holder)
    {
        if (runtime.CurrentHolder == holder)
            return;

        RemoveHolderRelation(notebook, runtime, log: false);
        runtime.CurrentHolder = holder;
        AddHolderNotebooks(holder, notebook);
        _adminLog.Add(
            LogType.Action,
            LogImpact.Low,
            $"{holder:player} temporarily acquired {notebook:entity}, owned by {runtime.OwnerEntity:entity}, in a hand.");
    }

    private void RemoveHolderRelation(
        EntityUid notebook,
        DeathNoteRuntimeComponent runtime,
        bool log = true)
    {
        if (runtime.CurrentHolder is not { } holder)
            return;

        runtime.CurrentHolder = null;
        RemoveHolderNotebooks(holder, notebook);

        if (log)
        {
            _adminLog.Add(
                LogType.Action,
                LogImpact.Low,
                $"{holder:entity} stopped temporarily holding {notebook:entity} in a hand.");
        }
    }

    private void RemoveOwnerRelation(
        EntityUid notebook,
        DeathNoteRuntimeComponent runtime,
        bool log = true)
    {
        if (runtime.OwnerEntity is not { } owner)
            return;

        runtime.OwnerEntity = null;
        RemoveOwnerNotebook(owner, notebook, GetOwnershipChannel(notebook));

        if (log)
        {
            _adminLog.Add(
                LogType.Action,
                LogImpact.Medium,
                $"{owner:entity} relinquished ownership of {notebook:entity}.");
        }
    }

    private DeathNoteOwnershipChannel GetOwnershipChannel(EntityUid notebook)
    {
        return TryComp(notebook, out DeathNoteComponent? component)
            ? component.OwnershipChannel
            : DeathNoteOwnershipChannel.First;
    }

    private void AddOwnerNotebook(EntityUid owner, EntityUid notebook, DeathNoteOwnershipChannel channel)
    {
        switch (channel)
        {
            case DeathNoteOwnershipChannel.First:
                EnsureComp<DeathNoteOwnerComponent>(owner).Notebooks.Add(notebook);
                break;
            case DeathNoteOwnershipChannel.Second:
                EnsureComp<DeathNoteOwner2Component>(owner).Notebooks.Add(notebook);
                break;
            case DeathNoteOwnershipChannel.Third:
                EnsureComp<DeathNoteOwner3Component>(owner).Notebooks.Add(notebook);
                break;
        }
    }

    private void AddHolderNotebook(EntityUid holder, EntityUid notebook, DeathNoteOwnershipChannel channel)
    {
        switch (channel)
        {
            case DeathNoteOwnershipChannel.First:
                EnsureComp<DeathNoteHolderComponent>(holder).Notebooks.Add(notebook);
                break;
            case DeathNoteOwnershipChannel.Second:
                EnsureComp<DeathNoteHolder2Component>(holder).Notebooks.Add(notebook);
                break;
            case DeathNoteOwnershipChannel.Third:
                EnsureComp<DeathNoteHolder3Component>(holder).Notebooks.Add(notebook);
                break;
        }
    }

    private void AddHolderNotebooks(EntityUid holder, EntityUid notebook)
    {
        if (TryComp(notebook, out DeathNoteComponent? component) &&
            component.GrantsAllShinigamiVisibility)
        {
            AddHolderNotebook(holder, notebook, DeathNoteOwnershipChannel.First);
            AddHolderNotebook(holder, notebook, DeathNoteOwnershipChannel.Second);
            AddHolderNotebook(holder, notebook, DeathNoteOwnershipChannel.Third);
            return;
        }

        AddHolderNotebook(holder, notebook, GetOwnershipChannel(notebook));
    }

    private void RemoveOwnerNotebook(EntityUid owner, EntityUid notebook, DeathNoteOwnershipChannel channel)
    {
        switch (channel)
        {
            case DeathNoteOwnershipChannel.First:
                if (TryComp(owner, out DeathNoteOwnerComponent? first))
                {
                    first.Notebooks.Remove(notebook);
                    if (first.Notebooks.Count == 0)
                        RemCompDeferred<DeathNoteOwnerComponent>(owner);
                }
                break;
            case DeathNoteOwnershipChannel.Second:
                if (TryComp(owner, out DeathNoteOwner2Component? second))
                {
                    second.Notebooks.Remove(notebook);
                    if (second.Notebooks.Count == 0)
                        RemCompDeferred<DeathNoteOwner2Component>(owner);
                }
                break;
            case DeathNoteOwnershipChannel.Third:
                if (TryComp(owner, out DeathNoteOwner3Component? third))
                {
                    third.Notebooks.Remove(notebook);
                    if (third.Notebooks.Count == 0)
                        RemCompDeferred<DeathNoteOwner3Component>(owner);
                }
                break;
        }
    }

    private void RemoveHolderNotebook(EntityUid holder, EntityUid notebook, DeathNoteOwnershipChannel channel)
    {
        switch (channel)
        {
            case DeathNoteOwnershipChannel.First:
                if (TryComp(holder, out DeathNoteHolderComponent? first))
                {
                    first.Notebooks.Remove(notebook);
                    if (first.Notebooks.Count == 0)
                        RemCompDeferred<DeathNoteHolderComponent>(holder);
                }
                break;
            case DeathNoteOwnershipChannel.Second:
                if (TryComp(holder, out DeathNoteHolder2Component? second))
                {
                    second.Notebooks.Remove(notebook);
                    if (second.Notebooks.Count == 0)
                        RemCompDeferred<DeathNoteHolder2Component>(holder);
                }
                break;
            case DeathNoteOwnershipChannel.Third:
                if (TryComp(holder, out DeathNoteHolder3Component? third))
                {
                    third.Notebooks.Remove(notebook);
                    if (third.Notebooks.Count == 0)
                        RemCompDeferred<DeathNoteHolder3Component>(holder);
                }
                break;
        }
    }

    private void RemoveHolderNotebooks(EntityUid holder, EntityUid notebook)
    {
        if (TryComp(notebook, out DeathNoteComponent? component) &&
            component.GrantsAllShinigamiVisibility)
        {
            RemoveHolderNotebook(holder, notebook, DeathNoteOwnershipChannel.First);
            RemoveHolderNotebook(holder, notebook, DeathNoteOwnershipChannel.Second);
            RemoveHolderNotebook(holder, notebook, DeathNoteOwnershipChannel.Third);
            return;
        }

        RemoveHolderNotebook(holder, notebook, GetOwnershipChannel(notebook));
    }
}
