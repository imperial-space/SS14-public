using System;
using System.Collections.Generic;
using Content.Server.Imperial.Medieval.Ships.Wave;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Drowning;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.PlayerDrowning;

public sealed class PlayerDrowningSystem : EntitySystem
{
    private const float DefaultReloadTimeSeconds = 1f;
    private const float SpawnShieldDuration = 45f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private TimeSpan _nextCheckTime;
    private readonly HashSet<MapId> _seaMaps = new();
    private readonly HashSet<Entity<TransformComponent>> _mapCandidates = new();

    public override void Initialize()
    {
        base.Initialize();
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);

        SubscribeLocalEvent<PlayerDrowningComponent, ComponentInit>(OnDrowningInit);
        SubscribeLocalEvent<PlayerDrowningComponent, ComponentShutdown>(OnDrowningShutdown);
        SubscribeLocalEvent<PlayerDrowningComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<UndrowableComponent, ComponentInit>(OnUndrowableInit);
    }

    private void OnUndrowableInit(Entity<UndrowableComponent> ent, ref ComponentInit args)
    {
        ResetDrowning(ent);
    }

    private void OnDrowningInit(Entity<PlayerDrowningComponent> ent, ref ComponentInit args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnDrowningShutdown(Entity<PlayerDrowningComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRefreshMovementSpeed(Entity<PlayerDrowningComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!HasComp<MobStateComponent>(ent) || HasComp<GhostComponent>(ent) || HasComp<UndrowableComponent>(ent))
            return;

        args.ModifySpeed(ent.Comp.SpeedModifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);

        CollectSeaMaps();
        if (_seaMaps.Count == 0)
            return;

        var resetQuery = EntityQueryEnumerator<PlayerDrowningComponent, TransformComponent>();
        while (resetQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (!_seaMaps.Contains(xform.MapID) || !ShouldDrown(uid, xform))
                ResetDrowning(uid);
        }

        foreach (var seaMapId in _seaMaps)
        {
            _mapCandidates.Clear();
            _lookup.GetEntitiesOnMap(seaMapId, _mapCandidates);

            foreach (var (uid, xform) in _mapCandidates)
            {
                if (TerminatingOrDeleted(uid))
                    continue;

                if (!ShouldDrown(uid, xform))
                    continue;

                ProcessDrowning(uid);
            }
        }
    }

    private void CollectSeaMaps()
    {
        _seaMaps.Clear();

        var query = EntityQueryEnumerator<SeaComponent>();
        while (query.MoveNext(out var uid, out var sea))
        {
            if (sea.Disabled)
                continue;

            _seaMaps.Add(_transform.GetMapId(uid));
        }
    }

    private bool ShouldDrown(EntityUid uid, TransformComponent xform)
    {
        return !xform.Anchored
               && !HasComp<MapComponent>(uid)
               && !HasComp<MapGridComponent>(uid)
               && !HasComp<WaveComponent>(uid)
               && !HasComp<UndrowableComponent>(uid)
               && !_container.IsEntityOrParentInContainer(uid)
               && !IsProtectedOrGhost(uid, xform)
               && !IsOnSolidTile(xform);
    }

    private void ResetDrowning(EntityUid uid)
    {
        if (!TryComp<PlayerDrowningComponent>(uid, out _))
            return;

        RemComp<PlayerDrowningComponent>(uid);
    }

    private void ProcessDrowning(EntityUid uid)
    {
        var drowner = EnsureComp<PlayerDrowningComponent>(uid);
        drowner.DrownTime += 1;

        if (TryComp<DrowningModifierComponent>(uid, out var modifier))
        {
            var mod = Math.Max(0.001f, modifier.ResistanceModifier);
            drowner.MaxDrownTime *= mod;
            drowner.DamageDrownDelay *= mod;
            drowner.DrowningDamage *= 1 / mod;
        }

        if (drowner.DrownTime >= drowner.DamageDrownDelay)
            _damageable.TryChangeDamage(uid, drowner.DrowningDamage, true, false);

        if (drowner.DrownTime < drowner.MaxDrownTime)
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Dead)
        {
            drowner.DrownTime = 0;
            drowner.DamageDrownDelay = 0;
            return;
        }

        SinkEntity(uid, drowner);
    }

    private void SinkEntity(EntityUid uid, PlayerDrowningComponent component)
    {
        var mapCoordinates = _transform.GetMapCoordinates(uid);
        var soundCoordinates = new EntityCoordinates(_map.GetMapOrInvalid(mapCoordinates.MapId), mapCoordinates.Position);
        QueueDel(uid);
        Spawn(component.SplashEffect, mapCoordinates);
        _audio.PlayPvs(_random.Pick(MedievalShipSounds.Drown), soundCoordinates);
    }

    private bool IsProtectedOrGhost(EntityUid uid, TransformComponent xform)
    {
        if (HasComp<GhostComponent>(uid) || IsEntityInvulnerable(uid))
            return true;

        var parent = xform.ParentUid;
        while (parent.IsValid() && !HasComp<MapComponent>(parent))
        {
            if (HasComp<GhostComponent>(parent) || IsEntityInvulnerable(parent))
                return true;

            if (!TryComp<TransformComponent>(parent, out var parentXform))
                break;

            parent = parentXform.ParentUid;
        }

        return false;
    }

    private bool IsEntityInvulnerable(EntityUid uid)
    {
        if (HasComp<GodmodeComponent>(uid))
            return true;

        return TryComp<ShieldOnStartupComponent>(uid, out var shield)
            && shield.Enabled
            && shield.Spawned + TimeSpan.FromSeconds(SpawnShieldDuration) >= _timing.CurTime;
    }

    private bool IsOnSolidTile(TransformComponent transform)
    {
        if (transform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        var tileIndices = _map.CoordinatesToTile(gridUid, gridComp, transform.Coordinates);

        return _map.TryGetTileRef(gridUid, gridComp, tileIndices, out var tile) && !tile.Tile.IsEmpty;
    }
}
