using System.Numerics;
using Content.Server.Anomaly.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Anomaly.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Movement.Events;
using Content.Shared.Teleportation.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Запускает отдельную карманную последовательность для цели, оставляя созданную
/// блюспейс-аномалию штатной для всех остальных игроков.
/// </summary>
public sealed class DeathNoteBluespaceAnomalyPresetHandlerSystem : EntitySystem, IDeathNotePresetHandler
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.BluespaceAnomaly;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeathNoteBluespaceVictimComponent, UpdateCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<DeathNoteBluespaceVictimComponent, ComponentShutdown>(OnVictimShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<DeathNoteBluespaceVictimComponent>();
        while (query.MoveNext(out var uid, out var victim))
            UpdateVictim(uid, victim);
    }

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target) ||
            parameters.EntityPrototype is not { } anomalyPrototype ||
            !_prototypes.TryIndex(anomalyPrototype, out EntityPrototype? prototype) ||
            !prototype.TryGetComponent<AnomalyComponent>(out _, EntityManager.ComponentFactory) ||
            !prototype.TryGetComponent<BluespaceAnomalyComponent>(out _, EntityManager.ComponentFactory))
        {
            return DeathNotePresetExecutionResult.Failed("Bluespace target or anomaly prototype is unavailable.");
        }

        if (HasComp<DeathNoteBluespaceVictimComponent>(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target is already in a Death Note bluespace sequence.");

        if (parameters.BluespaceTeleportDelay < TimeSpan.Zero ||
            parameters.BluespaceCollapseDelay <= parameters.BluespaceTeleportDelay ||
            parameters.BluespaceReturnDelay <= parameters.BluespaceCollapseDelay)
        {
            return DeathNotePresetExecutionResult.Failed("Bluespace timing or damage parameters are invalid.");
        }

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Bluespace damage parameters are invalid.");

        var xform = Transform(context.Target);
        var mapCoordinates = _transform.GetMapCoordinates((context.Target, xform));
        if (mapCoordinates.MapId == MapId.Nullspace)
            return DeathNotePresetExecutionResult.Failed("Bluespace target is in nullspace.");

        var anomaly = Spawn(anomalyPrototype, xform.Coordinates);
        var victim = EnsureComp<DeathNoteBluespaceVictimComponent>(context.Target);
        victim.ReturnCoordinates = xform.Coordinates;
        victim.ReturnMapCoordinates = mapCoordinates;
        victim.Anomaly = anomaly;
        victim.TeleportAt = _timing.CurTime + parameters.BluespaceTeleportDelay;
        victim.CollapseAt = _timing.CurTime + parameters.BluespaceCollapseDelay;
        victim.ReturnAt = _timing.CurTime + parameters.BluespaceReturnDelay;
        victim.Damage = damage;
        if (!HasComp<PortalTimeoutComponent>(context.Target))
        {
            var portalTimeout = EnsureComp<PortalTimeoutComponent>(context.Target);
            portalTimeout.EnteredPortal = anomaly;
            victim.OwnsPortalTimeout = true;
            Dirty(context.Target, portalTimeout);
        }

        return DeathNotePresetExecutionResult.Succeeded("Standard bluespace anomaly and target-only pocket sequence started.");
    }

    private void UpdateVictim(EntityUid uid, DeathNoteBluespaceVictimComponent victim)
    {
        if (!victim.Teleported && _timing.CurTime >= victim.TeleportAt)
        {
            victim.PocketMap = _maps.CreateMap();
            _transform.SetCoordinates(uid, new EntityCoordinates(victim.PocketMap.Value, Vector2.Zero));
            RemoveOwnedPortalTimeout(uid, victim);
            victim.Teleported = true;
            _blocker.UpdateCanMove(uid);
        }

        if (!victim.Collapsed && _timing.CurTime >= victim.CollapseAt)
        {
            _damageable.TryChangeDamage(
                uid,
                new DamageSpecifier(victim.Damage),
                ignoreResistances: true,
                interruptsDoAfters: true,
                origin: Deleted(victim.Anomaly) ? null : victim.Anomaly,
                ignoreGlobalModifiers: true);
            victim.Collapsed = true;
        }

        if (_timing.CurTime < victim.ReturnAt)
            return;

        if (!Deleted(victim.ReturnCoordinates.EntityId))
            _transform.SetCoordinates(uid, victim.ReturnCoordinates);
        else if (!Deleted(victim.Anomaly))
            _transform.SetCoordinates(uid, Transform(victim.Anomaly).Coordinates);
        else
            _transform.SetMapCoordinates(uid, victim.ReturnMapCoordinates);

        if (victim.PocketMap is { } map && !Deleted(map))
            QueueDel(map);
        victim.PocketMap = null;
        RemCompDeferred<DeathNoteBluespaceVictimComponent>(uid);
        _blocker.UpdateCanMove(uid);
    }

    private void OnCanMove(
        Entity<DeathNoteBluespaceVictimComponent> ent,
        ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Teleported)
            args.Cancel();
    }

    private void OnVictimShutdown(
        Entity<DeathNoteBluespaceVictimComponent> ent,
        ref ComponentShutdown args)
    {
        if (ent.Comp.PocketMap is { } map && !Deleted(map))
            QueueDel(map);
        ent.Comp.PocketMap = null;

        if (!Terminating(ent.Owner))
        {
            RemoveOwnedPortalTimeout(ent.Owner, ent.Comp);
            _blocker.UpdateCanMove(ent.Owner);
        }
    }

    private void RemoveOwnedPortalTimeout(
        EntityUid uid,
        DeathNoteBluespaceVictimComponent victim)
    {
        if (!victim.OwnsPortalTimeout)
            return;

        if (TryComp(uid, out PortalTimeoutComponent? timeout) &&
            timeout.EnteredPortal == victim.Anomaly)
        {
            RemComp<PortalTimeoutComponent>(uid);
        }

        victim.OwnsPortalTimeout = false;
    }
}
