using Content.Server.Doors;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Server.Wires;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Mobs.Components;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteGuidedScenarioSystem
{
    private void OnDeathNoteAirlockOpening(
        Entity<DoorComponent> ent,
        ref BeforeDoorOpenedEvent args)
    {
        if (args.Cancelled ||
            args.User is not { } user ||
            !TryComp(user, out DeathNoteGuidedScenarioComponent? guide) ||
            guide.Scenario != DeathNoteGuidedScenarioType.AirlockAccident ||
            guide.Triggered ||
            IsGuideExpired(guide) ||
            !TryComp(ent.Owner, out AirlockComponent? airlock) ||
            !_doors.HasAccess(ent.Owner, user, ent.Comp))
        {
            return;
        }

        TryArmDeadlyAirlock((user, guide), (ent.Owner, airlock, ent.Comp));
    }

    private bool TryArmDeadlyAirlock(
        Entity<DeathNoteGuidedScenarioComponent> target,
        Entity<AirlockComponent, DoorComponent> airlock)
    {
        if (TryComp(airlock.Owner, out DeathNoteDeadlyAirlockComponent? existingDeadly) &&
            (existingDeadly.EntryId != target.Comp.EntryId || existingDeadly.Target != target.Owner))
        {
            return false;
        }

        Wire? safetyWire = null;
        foreach (var wire in _wires.TryGetWires<DoorSafetyWireAction>(airlock.Owner))
        {
            safetyWire = wire;
            break;
        }

        if (safetyWire == null)
            return false;

        if (!DeathNoteDamageHelper.TryCreate(
                target.Comp.MinimumDamage,
                target.Comp.MaximumDamage,
                _random,
                out var deadlyDamage) ||
            !TryStartGuidedEffect(target))
        {
            return false;
        }

        var deadly = EnsureComp<DeathNoteDeadlyAirlockComponent>(airlock.Owner);
        deadly.EntryId = target.Comp.EntryId;
        deadly.Target = target.Owner;
        deadly.Damage = deadlyDamage;
        deadly.LethalImpactApplied = false;
        deadly.ForceCloseAt = null;
        deadly.ForceCloseDelay = target.Comp.AirlockForceCloseDelay;
        deadly.AutoCloseDelayModifier = target.Comp.AirlockAutoCloseDelayModifier;
        deadly.ImpactRadius = target.Comp.DoorwayImpactRadius;

        if (!deadly.TimingsOverridden)
        {
            deadly.OriginalCloseTimeOne = airlock.Comp2.CloseTimeOne;
            deadly.OriginalCloseTimeTwo = airlock.Comp2.CloseTimeTwo;
            deadly.OriginalAutoCloseDelayModifier = airlock.Comp1.AutoCloseDelayModifier;
            deadly.OriginalCanCrush = airlock.Comp2.CanCrush;
            deadly.TimingsOverridden = true;
        }

        airlock.Comp2.CloseTimeOne = target.Comp.DoorCloseStageDuration;
        airlock.Comp2.CloseTimeTwo = target.Comp.DoorCloseStageDuration;
        _airlocks.SetAutoCloseDelayModifier(airlock.Comp1, deadly.AutoCloseDelayModifier);

        // Cut the real safety wire rather than only changing AirlockComponent.Safety.
        // The regular wire-mending action then remains the sole way to repair the door.
        if (safetyWire.Action == null ||
            !safetyWire.Action.Cut(EntityUid.Invalid, safetyWire))
        {
            RestoreAirlock(airlock.Owner, deadly, airlock.Comp2, airlock.Comp1);
            RemComp<DeathNoteDeadlyAirlockComponent>(airlock.Owner);
            return false;
        }

        safetyWire.IsCut = true;

        // WiresSystem has no public UI invalidation method. Setting and immediately
        // removing a private state key refreshes the existing wire UI state without
        // leaving Death Note data on the airlock.
        _wires.SetData(airlock.Owner, DeathNoteWireStateKey.SafetyCut, true);
        _wires.RemoveData(airlock.Owner, DeathNoteWireStateKey.SafetyCut);
        Dirty(airlock.Owner, airlock.Comp1);
        Dirty(airlock.Owner, airlock.Comp2);

        target.Comp.Triggered = true;
        RemCompDeferred<DeathNoteGuidedScenarioComponent>(target.Owner);
        return true;
    }

    private enum DeathNoteWireStateKey : byte
    {
        SafetyCut,
    }

    private void OnDeadlyAirlockClosing(
        Entity<DeathNoteDeadlyAirlockComponent> ent,
        ref BeforeDoorClosedEvent args)
    {
        if (!args.Partial || args.Cancelled)
            return;

        if (ent.Comp.LethalImpactApplied)
            return;

        TryApplyDeadlyAirlockImpact(ent.Owner, ent.Comp);
    }

    private void TryApplyDeadlyAirlockImpact(EntityUid airlockUid, DeathNoteDeadlyAirlockComponent deadly)
    {
        if (deadly.LethalImpactApplied || Deleted(deadly.Target) ||
            !IsTargetInDoorway(airlockUid, deadly.Target, deadly.ImpactRadius))
        {
            return;
        }

        if (TryComp(deadly.Target, out MobStateComponent? targetState) &&
            _mobState.IsDead(deadly.Target, targetState))
        {
            FinishDeadlyAirlockImpact(airlockUid, deadly);
            return;
        }

        _damageable.TryChangeDamage(
            deadly.Target,
            new Content.Shared.Damage.DamageSpecifier(deadly.Damage),
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: airlockUid,
            ignoreGlobalModifiers: true);
        FinishDeadlyAirlockImpact(airlockUid, deadly);
    }

    private void OnDeadlyAirlockStateChanged(
        Entity<DeathNoteDeadlyAirlockComponent> ent,
        ref DoorStateChangedEvent args)
    {
        if (args.State == DoorState.Closing)
        {
            TryApplyDeadlyAirlockImpact(ent.Owner, ent.Comp);
            return;
        }

        if (args.State == DoorState.Open && !ent.Comp.LethalImpactApplied)
        {
            ent.Comp.ForceCloseAt = _timing.CurTime + ent.Comp.ForceCloseDelay;
            return;
        }

        // If another interaction reverses the door before the lethal impact,
        // arm it again when it next finishes opening.
        if (args.State == DoorState.Opening && !ent.Comp.LethalImpactApplied)
            ent.Comp.ForceCloseAt = null;
    }

    private void FinishDeadlyAirlockImpact(EntityUid uid, DeathNoteDeadlyAirlockComponent deadly)
    {
        deadly.LethalImpactApplied = true;
        deadly.ForceCloseAt = null;

        if (!TryComp(uid, out DoorComponent? door) || !TryComp(uid, out AirlockComponent? airlock))
            return;

        // Тетрадь наносит только один смертельный удар. Если оставить раздавливание включённым,
        // штатный отскок небезопасного шлюза продолжит повреждать уже мёртвое тело.
        door.CanCrush = false;
        RestoreAirlockTimings(deadly, door, airlock);
        Dirty(uid, door);
    }

    private bool IsTargetInDoorway(EntityUid airlock, EntityUid target, float impactRadius)
    {
        if (Deleted(target))
            return false;

        foreach (var colliding in _doors.GetColliding(airlock))
        {
            if (colliding == target)
                return true;
        }

        // Открытый шлюз временно не участвует в коллизиях, поэтому на кадре частичного
        // закрытия используем ограниченную пространственную проверку только назначенной цели.
        var airlockCoordinates = _transform.GetMapCoordinates(airlock);
        var targetCoordinates = _transform.GetMapCoordinates(target);
        return airlockCoordinates.MapId == targetCoordinates.MapId &&
               (airlockCoordinates.Position - targetCoordinates.Position).LengthSquared() <=
               impactRadius * impactRadius;
    }

    private void OnDeadlyAirlockShutdown(
        Entity<DeathNoteDeadlyAirlockComponent> ent,
        ref ComponentShutdown args)
    {
        RestoreAirlock(ent.Owner, ent.Comp);
    }

    private void RestoreAirlock(
        EntityUid uid,
        DeathNoteDeadlyAirlockComponent deadly,
        DoorComponent? door = null,
        AirlockComponent? airlock = null)
    {
        if (!deadly.TimingsOverridden ||
            !Resolve(uid, ref door, false) ||
            !Resolve(uid, ref airlock, false))
        {
            return;
        }

        door.CloseTimeOne = deadly.OriginalCloseTimeOne;
        door.CloseTimeTwo = deadly.OriginalCloseTimeTwo;
        _airlocks.SetAutoCloseDelayModifier(airlock, deadly.OriginalAutoCloseDelayModifier);
        door.CanCrush = deadly.OriginalCanCrush;
        deadly.TimingsOverridden = false;
        Dirty(uid, door);
    }

    private void RestoreAirlockTimings(
        DeathNoteDeadlyAirlockComponent deadly,
        DoorComponent door,
        AirlockComponent airlock)
    {
        if (!deadly.TimingsOverridden)
            return;

        door.CloseTimeOne = deadly.OriginalCloseTimeOne;
        door.CloseTimeTwo = deadly.OriginalCloseTimeTwo;
        _airlocks.SetAutoCloseDelayModifier(airlock, deadly.OriginalAutoCloseDelayModifier);
    }
}
