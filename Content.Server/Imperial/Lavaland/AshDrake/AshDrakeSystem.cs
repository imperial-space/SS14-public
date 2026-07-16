using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Imperial.Lavaland.AshDrake;

public sealed class AshDrakeSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly Dictionary<EntityUid, List<PendingTileDamage>> _pendingDamage = new();

    private readonly record struct PendingTileDamage(
        EntityCoordinates Tile,
        TimeSpan TriggerTime,
        float Damage,
        string EffectProto,
        bool SpawnEffectOnTrigger);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshDrakeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AshDrakeComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<AshDrakeComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AshDrakeComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<AshDrakeComponent, AmmoShotEvent>(OnGunShot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Deal continuous damage to every active fire tile each 0.15 s
        var fireQuery = EntityQueryEnumerator<AshDrakeFireTileComponent>();
        while (fireQuery.MoveNext(out var tileUid, out var fireTile))
        {
            if (_timing.CurTime < fireTile.NextDamageTime)
                continue;
            if (Exists(fireTile.DrakeUid))
                DamageEntitiesOnTile(fireTile.DrakeUid, Transform(tileUid).Coordinates, fireTile.Damage);
            fireTile.NextDamageTime = _timing.CurTime + TimeSpan.FromSeconds(0.15);
        }

        var query = EntityQueryEnumerator<AshDrakeComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damageable, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            ProcessPendingTileDamage(uid);
            ProcessActiveMeteors(uid, comp);

            if (comp.IsFireArenaActive)
            {
                ProcessFireArena(uid, comp);
                continue;
            }

            if (comp.IsSwooping)
            {
                ProcessSwoop(uid, comp);
                continue; // no other attacks during swoop
            }

            var totalDamage = _damageable.GetPositiveDamage((uid, damageable)).GetTotal().Float();
            var healthRatio = totalDamage / comp.MaxHp;
            var belowHalfHp = healthRatio >= 0.5f;

            if (!belowHalfHp)
                comp.LowHpSwoopsSinceLastArena = 0;

            // Fire cone
            if (_timing.CurTime >= comp.NextFireConeTime &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var coneTarget))
                DoFireCone(uid, coneTarget, comp, healthRatio);

            // Meteor rain
            if (_timing.CurTime >= comp.NextMeteorTime &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var meteorTarget))
                DoMeteorRain(uid, meteorTarget, comp, healthRatio);

            // At or below 50% HP: circular fire breath
            if (belowHalfHp && _timing.CurTime >= comp.NextCircularFireBreathTime &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var circularTarget))
                DoCircularFireBreath(uid, circularTarget, comp, healthRatio);

            // At or below 50% HP: two normal swoops, then arena on the third
            if (belowHalfHp)
            {
                if (_timing.CurTime >= comp.NextSwoopTime &&
                    TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var lowHpFlightTarget))
                {
                    if (comp.LowHpSwoopsSinceLastArena >= 2 &&
                        _timing.CurTime >= comp.NextFireArenaTime)
                    {
                        DoFireArena(uid, lowHpFlightTarget, comp);
                    }
                    else
                    {
                        StartSwoop(uid, lowHpFlightTarget, comp);
                        comp.LowHpSwoopsSinceLastArena++;
                    }
                }
            }
            else if (_timing.CurTime >= comp.NextSwoopTime &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var swoopTarget))
            {
                StartSwoop(uid, swoopTarget, comp);
            }
        }
    }

    // ── Existing melee-counter shot system ───────────────────────────────────

    private void OnMapInit(EntityUid uid, AshDrakeComponent component, MapInitEvent args)
    {
        component.MeleeHitsSinceLastShot = 0;
        component.NextShotAtMeleeHits = component.FirstShotMeleeHits;
        UpdateVisual(uid, component);
    }

    private void OnMeleeHit(EntityUid uid, AshDrakeComponent component, MeleeHitEvent args)
    {
        if (component.IsFireArenaActive)
            return;

        if (args.IsHit)
        {
            component.MeleeHitsSinceLastShot++;
            // Play melee hit sound
            _audio.PlayPvs(component.MeleeAttackSound, uid);
        }

        UpdateVisual(uid, component);
    }

    private void OnShotAttempted(EntityUid uid, AshDrakeComponent component, ref ShotAttemptedEvent args)
    {
        if (component.IsFireArenaActive)
        {
            args.Cancel();
            return;
        }

        if (IsShotReady(component))
            return;

        args.Cancel();
    }

    private void OnAttemptMelee(EntityUid uid, AshDrakeComponent component, ref AttemptMeleeEvent args)
    {
        if (!component.IsFireArenaActive)
            return;

        args.Cancelled = true;
    }

    private void OnGunShot(EntityUid uid, AshDrakeComponent component, AmmoShotEvent args)
    {
        component.MeleeHitsSinceLastShot = 0;
        component.RollNextThreshold(_random);
        UpdateVisual(uid, component);
    }

    private static bool IsShotReady(AshDrakeComponent component)
        => component.MeleeHitsSinceLastShot >= component.NextShotAtMeleeHits;

    private void UpdateVisual(EntityUid uid, AshDrakeComponent component)
    {
        var unfurled = IsShotReady(component);
        _appearance.SetData(uid, AshDrakeVisuals.Unfurled, unfurled);
    }

    // ── Fire cone ─────────────────────────────────────────────────────────────

    private void DoFireCone(EntityUid uid, EntityUid target, AshDrakeComponent comp, float healthRatio)
    {
        var origin = SnapToTile(Transform(uid).Coordinates);
        var targetPos = Transform(target).Coordinates;

        var dx = targetPos.X - origin.X;
        var dy = targetPos.Y - origin.Y;
        var baseAngle = MathF.Atan2(dy, dx);

        var half = comp.FireConeSpreadDeg * (MathF.PI / 180f) / 2f;
        var range = (int)comp.FireConeRange;
        var rayCount = comp.FireConeRayCount;

        // Play sound
        _audio.PlayPvs(comp.FireConeSound, uid);

        for (var i = 0; i < rayCount; i++)
        {
            var t = rayCount <= 1 ? 0f : (float)i / (rayCount - 1) - 0.5f;
            var rayAngle = baseAngle + t * 2f * half;
            var dir = new Vector2(MathF.Cos(rayAngle), MathF.Sin(rayAngle));

            SpawnSnakeRay(uid, origin, dir, range, comp, comp.FireConeDamage, 0f, healthRatio);
        }

        comp.NextFireConeTime = _timing.CurTime + TimeSpan.FromSeconds(comp.FireConeCooldown);
    }

    // ── Meteor rain ────────────────────────────────────────────────────────────

    private void DoMeteorRain(EntityUid uid, EntityUid target, AshDrakeComponent comp, float healthRatio)
    {
        var drakePos = SnapToTile(Transform(uid).Coordinates);
        var center = SnapToTile(Transform(target).Coordinates);
        var r = (int)comp.MeteorRadius;

        // Play sound
        _audio.PlayPvs(comp.MeteorRainSound, uid);

        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                if (!_random.Prob(comp.MeteorSpawnChance))
                    continue;

                var tile = SnapToTile(center.Offset(new Vector2(x, y)));

                // Travel time based on distance from drake to tile
                var travelTime = 0.5f;
                if (drakePos.TryDistance(EntityManager, tile, out var dist))
                    travelTime = Math.Max(0.3f, dist / comp.MeteorTravelSpeed);

                // 1. Crosshair warning at destination
                var warningUid = Spawn(comp.MeteorWarningPrototype, tile);

                // 2. Fireball starts at drake position
                var fireballUid = Spawn(comp.MeteorFireballPrototype, drakePos);

                // 3. Track until impact
                comp.ActiveMeteors.Add(new InFlightMeteor
                {
                    FireballUid = fireballUid,
                    WarningUid = warningUid,
                    StartPos = drakePos,
                    EndPos = tile,
                    StartTime = _timing.CurTime,
                    TravelDuration = TimeSpan.FromSeconds(travelTime),
                    Damage = comp.MeteorDamage,
                });
            }
        }

        comp.NextMeteorTime = _timing.CurTime + TimeSpan.FromSeconds(comp.MeteorCooldown);
    }

    // ── Swoop ────────────────────────────────────────────────────────────────

    private void StartSwoop(EntityUid uid, EntityUid target, AshDrakeComponent comp)
    {
        comp.IsSwooping = true;
        comp.SwoopTarget = target;
        comp.SwoopStartTime = _timing.CurTime;

        var origin = SnapToTile(Transform(uid).Coordinates);
        var destination = Exists(target)
            ? SnapToTile(Transform(target).Coordinates)
            : origin;

        comp.SwoopStartCoordinates = origin;
        comp.SwoopDestinationCoordinates = destination;

        comp.SwoopWarningTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SwoopFlightDuration);
        comp.SwoopLandTime = comp.SwoopWarningTime + TimeSpan.FromSeconds(comp.SwoopLandingDelay);
        comp.NextSwoopTrailTime = _timing.CurTime;

        if (comp.SwoopShadowUid.Valid && Exists(comp.SwoopShadowUid))
            Del(comp.SwoopShadowUid);

        // Spawn shadow at drake's current position — it will fly toward target
        comp.SwoopShadowUid = Spawn(comp.SwoopShadowPrototype, origin);

        if (comp.SwoopWarningUid.Valid && Exists(comp.SwoopWarningUid))
            Del(comp.SwoopWarningUid);

        // Spawn landing warning IMMEDIATELY so player has max time to react
        comp.SwoopWarningSpawned = true;
        comp.SwoopWarningUid = Spawn(comp.SwoopLandingWarningPrototype, destination);

        // Hide the real drake body — only the shadow will be visible during flight
        _appearance.SetData(uid, AshDrakeVisuals.Flying, true);
        _appearance.SetData(uid, AshDrakeVisuals.Unfurled, false);
        // Play swoop sound
        _audio.PlayPvs(comp.MeleeAttackSound, uid);
    }

    private void ProcessSwoop(EntityUid uid, AshDrakeComponent comp)
    {
        if (_timing.CurTime < comp.SwoopWarningTime)
        {
            var elapsed = (_timing.CurTime - comp.SwoopStartTime).TotalSeconds;
            var progress = Math.Clamp((float)(elapsed / comp.SwoopFlightDuration), 0f, 1f);

            // Move only the shadow — use smooth float interpolation (no tile rounding)
            if (comp.SwoopShadowUid.Valid && Exists(comp.SwoopShadowUid))
            {
                var shadowPos = LerpSmooth(comp.SwoopStartCoordinates, comp.SwoopDestinationCoordinates, progress);
                _transform.SetCoordinates(comp.SwoopShadowUid, shadowPos);

                if (_timing.CurTime >= comp.NextSwoopTrailTime)
                {
                    Spawn(comp.FireEffectPrototype, shadowPos);
                    comp.NextSwoopTrailTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SwoopTrailInterval);
                }
            }

            // Drake body stays hidden at its original position — NPC movement doesn't matter
            return;
        }

        if (!comp.SwoopWarningSpawned)
        {
            comp.SwoopWarningSpawned = true;
            comp.SwoopWarningUid = Spawn(comp.SwoopLandingWarningPrototype, comp.SwoopDestinationCoordinates);
        }

        if (_timing.CurTime >= comp.SwoopLandTime)
            ExecuteSwoopLand(uid, comp);
    }

    private void ExecuteSwoopLand(EntityUid uid, AshDrakeComponent comp)
    {
        comp.IsSwooping = false;

        if (comp.SwoopShadowUid.Valid && Exists(comp.SwoopShadowUid))
            Del(comp.SwoopShadowUid);
        comp.SwoopShadowUid = EntityUid.Invalid;

        if (comp.SwoopWarningUid.Valid && Exists(comp.SwoopWarningUid))
            Del(comp.SwoopWarningUid);
        comp.SwoopWarningUid = EntityUid.Invalid;

        // Show the drake again, then teleport to landing spot
        _appearance.SetData(uid, AshDrakeVisuals.Flying, false);

        var landPos = comp.SwoopDestinationCoordinates;
        _transform.SetCoordinates(uid, landPos);

        // Play landing sound
        _audio.PlayPvs(comp.MeleeAttackSound, uid);

        // AoE fire tiles in radius around landing spot
        var r = (int)comp.SwoopAoeRadius;
        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                if (x * x + y * y > r * r)
                    continue; // circular
                var tile = SnapToTile(landPos.Offset(new Vector2(x, y)));
                SpawnDamageTile(uid, tile, comp.FireEffectPrototype, comp.SwoopDamage, comp.TileDamageDelay);
            }
        }

        comp.SwoopTarget = EntityUid.Invalid;
        comp.NextSwoopTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SwoopCooldown);
        UpdateVisual(uid, comp);
    }

    // ── Circular Fire Breath (below 50% HP) ───────────────────────────────────

    private void DoCircularFireBreath(EntityUid uid, EntityUid target, AshDrakeComponent comp, float healthRatio)
    {
        var origin = SnapToTile(Transform(uid).Coordinates);
        var range = (int)comp.CircularFireBreathRange;

        // Play sound
        _audio.PlayPvs(comp.FireConeSound, uid);

        // 360° in 8 directions (cross + diagonals)
        var directions = new[]
        {
            new Vector2(1, 0),   // East
            new Vector2(-1, 0),  // West
            new Vector2(0, 1),   // North
            new Vector2(0, -1),  // South
            new Vector2(1, 1),   // NE
            new Vector2(-1, 1),  // NW
            new Vector2(1, -1),  // SE
            new Vector2(-1, -1)  // SW
        };

        for (var repeat = 0; repeat < comp.CircularFireBreathRepeats; repeat++)
        {
            foreach (var dir in directions)
            {
                SpawnSnakeRay(uid,
                    origin,
                    Vector2.Normalize(dir),
                    range,
                    comp,
                    comp.CircularFireBreathDamage,
                    repeat * comp.CircularFireBreathRepeatDelay,
                    healthRatio);
            }
        }

        comp.NextCircularFireBreathTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CircularFireBreathCooldown);
    }

    // ── Fire Arena (below 50% HP, replaces swoop) ────────────────────────────

    private void DoFireArena(EntityUid uid, EntityUid target, AshDrakeComponent comp)
    {
        var center = SnapToTile(Transform(target).Coordinates);

        comp.IsFireArenaActive = true;
        comp.FireArenaPhase = FireArenaPhase.None;
        comp.FireArenaCenterCoordinates = center;
        comp.FireArenaMarkerTile = center;
        comp.FireArenaCompletedRounds = 0;
        comp.LowHpSwoopsSinceLastArena = 0;

        if (comp.FireArenaMarkerUid.Valid && Exists(comp.FireArenaMarkerUid))
            Del(comp.FireArenaMarkerUid);
        comp.FireArenaMarkerUid = EntityUid.Invalid;

        if (comp.FireArenaWallUids.Count > 0)
        {
            foreach (var wallUid in comp.FireArenaWallUids)
            {
                if (wallUid.Valid && Exists(wallUid))
                    Del(wallUid);
            }
            comp.FireArenaWallUids.Clear();
        }

        if (comp.SwoopShadowUid.Valid && Exists(comp.SwoopShadowUid))
            Del(comp.SwoopShadowUid);
        comp.SwoopShadowUid = Spawn(comp.SwoopShadowPrototype, center);

        // Keep the real drake exactly at arena center while visually flying.
        _transform.SetCoordinates(uid, center);

        // Keep drake in the air over the center while arena minigame is active.
        _appearance.SetData(uid, AshDrakeVisuals.Flying, true);
        _appearance.SetData(uid, AshDrakeVisuals.Unfurled, false);

        SpawnFireArenaWalls(comp);
        _audio.PlayPvs(comp.FireConeSound, uid);
        StartNextFireArenaRound(comp);
    }

    private void ProcessFireArena(EntityUid uid, AshDrakeComponent comp)
    {
        _transform.SetCoordinates(uid, comp.FireArenaCenterCoordinates);

        switch (comp.FireArenaPhase)
        {
            case FireArenaPhase.WaitingForMarker:
                if (_timing.CurTime < comp.FireArenaMarkerEndTime)
                    return;

                SpawnFireArenaFlames(uid, comp);
                comp.FireArenaFlameEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.FireArenaFlameDuration);
                comp.FireArenaPhase = FireArenaPhase.FlamesActive;
                break;

            case FireArenaPhase.FlamesActive:
                if (_timing.CurTime < comp.FireArenaFlameEndTime)
                    return;

                if (comp.FireArenaMarkerUid.Valid && Exists(comp.FireArenaMarkerUid))
                    Del(comp.FireArenaMarkerUid);
                comp.FireArenaMarkerUid = EntityUid.Invalid;

                comp.FireArenaCompletedRounds++;
                if (comp.FireArenaCompletedRounds >= comp.FireArenaRounds)
                {
                    EndFireArena(uid, comp);
                    return;
                }

                StartNextFireArenaRound(comp);
                break;
        }
    }

    private void StartNextFireArenaRound(AshDrakeComponent comp)
    {
        var playableRadius = Math.Max(1, comp.FireArenaRadius - 1);
        var markerTile = comp.FireArenaCompletedRounds == 0
            ? PickRandomArenaTile(comp.FireArenaCenterCoordinates, playableRadius)
            : PickArenaMarkerTileAtDistance(
                comp.FireArenaCenterCoordinates,
                comp.FireArenaMarkerTile,
                playableRadius,
                comp.FireArenaMarkerStepDistance);

        comp.FireArenaMarkerTile = markerTile;

        if (comp.FireArenaMarkerUid.Valid && Exists(comp.FireArenaMarkerUid))
            Del(comp.FireArenaMarkerUid);

        comp.FireArenaMarkerUid = Spawn(comp.FireArenaMarkerPrototype, markerTile);
        comp.FireArenaMarkerEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.FireArenaMarkerDuration);
        comp.FireArenaPhase = FireArenaPhase.WaitingForMarker;
    }

    private void SpawnFireArenaFlames(EntityUid uid, AshDrakeComponent comp)
    {
        var center = comp.FireArenaCenterCoordinates;
        var radius = comp.FireArenaRadius;

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (IsPerimeterOffset(x, y, radius))
                    continue;

                var tile = SnapToTile(center.Offset(new Vector2(x, y)));
                if (IsSameTile(tile, comp.FireArenaMarkerTile))
                    continue;

                SpawnDamageTile(uid, tile, comp.FireArenaEffectPrototype, comp.FireArenaDamage, 0f);
            }
        }
    }

    private void EndFireArena(EntityUid uid, AshDrakeComponent comp)
    {
        comp.IsFireArenaActive = false;
        comp.FireArenaPhase = FireArenaPhase.None;

        if (comp.FireArenaMarkerUid.Valid && Exists(comp.FireArenaMarkerUid))
            Del(comp.FireArenaMarkerUid);
        comp.FireArenaMarkerUid = EntityUid.Invalid;

        if (comp.FireArenaWallUids.Count > 0)
        {
            foreach (var wallUid in comp.FireArenaWallUids)
            {
                if (wallUid.Valid && Exists(wallUid))
                    Del(wallUid);
            }
            comp.FireArenaWallUids.Clear();
        }

        comp.NextFireArenaTime = _timing.CurTime + TimeSpan.FromSeconds(comp.FireArenaCooldown);

        // Reuse standard swoop landing logic (shadow removal, visibility restore, impact AoE).
        comp.SwoopDestinationCoordinates = comp.FireArenaCenterCoordinates;
        ExecuteSwoopLand(uid, comp);
    }

    private void SpawnFireArenaWalls(AshDrakeComponent comp)
    {
        var center = comp.FireArenaCenterCoordinates;
        var radius = comp.FireArenaRadius;

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (!IsPerimeterOffset(x, y, radius))
                    continue;

                var tile = SnapToTile(center.Offset(new Vector2(x, y)));
                var wallUid = Spawn(comp.FireArenaWallPrototype, tile);
                comp.FireArenaWallUids.Add(wallUid);
            }
        }
    }

    private EntityCoordinates PickRandomArenaTile(EntityCoordinates center, int radius)
    {
        var dx = _random.Next(-radius, radius + 1);
        var dy = _random.Next(-radius, radius + 1);
        return SnapToTile(center.Offset(new Vector2(dx, dy)));
    }

    private EntityCoordinates PickArenaMarkerTileAtDistance(
        EntityCoordinates center,
        EntityCoordinates previous,
        int radius,
        int distance)
    {
        var candidates = new List<EntityCoordinates>();

        for (var dx = -distance; dx <= distance; dx++)
        {
            for (var dy = -distance; dy <= distance; dy++)
            {
                if (MathF.Abs(dx) + MathF.Abs(dy) != distance)
                    continue;

                var x = previous.X + dx;
                var y = previous.Y + dy;

                if (MathF.Abs(x - center.X) > radius || MathF.Abs(y - center.Y) > radius)
                    continue;

                candidates.Add(SnapToTile(new EntityCoordinates(center.EntityId, x, y)));
            }
        }

        if (candidates.Count == 0)
            return PickRandomArenaTile(center, radius);

        return candidates[_random.Next(candidates.Count)];
    }

    private static bool IsPerimeterOffset(int x, int y, int radius)
        => MathF.Abs(x) == radius || MathF.Abs(y) == radius;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryFindNearbyPlayer(EntityUid uid, float range, out EntityUid result)
    {
        result = EntityUid.Invalid;
        var myPos = Transform(uid).Coordinates;
        var best = float.MaxValue;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } candidate)
                continue;

            if (candidate == uid)
                continue;

            if (!Exists(candidate))
                continue;

            if (!TryComp<MobStateComponent>(candidate, out var ms) || ms.CurrentState != MobState.Alive)
                continue;

            if (!myPos.TryDistance(EntityManager, Transform(candidate).Coordinates, out var dist))
                continue;

            if (dist > range || dist >= best)
                continue;

            best = dist;
            result = candidate;
        }

        return result.Valid;
    }

    private void ProcessActiveMeteors(EntityUid uid, AshDrakeComponent comp)
    {
        for (var i = comp.ActiveMeteors.Count - 1; i >= 0; i--)
        {
            var meteor = comp.ActiveMeteors[i];
            var elapsed = (_timing.CurTime - meteor.StartTime).TotalSeconds;
            var progress = Math.Clamp((float)(elapsed / meteor.TravelDuration.TotalSeconds), 0f, 1f);

            if (progress < 1f)
            {
                // Animate fireball toward destination
                if (meteor.FireballUid.Valid && Exists(meteor.FireballUid))
                {
                    var pos = LerpSmooth(meteor.StartPos, meteor.EndPos, progress);
                    _transform.SetCoordinates(meteor.FireballUid, pos);
                }
            }
            else
            {
                // Impact — delete projectile and warning, spawn fire + queue damage
                if (meteor.FireballUid.Valid && Exists(meteor.FireballUid))
                    Del(meteor.FireballUid);
                if (meteor.WarningUid.Valid && Exists(meteor.WarningUid))
                    Del(meteor.WarningUid);

                SpawnDamageTile(uid, meteor.EndPos, comp.FireEffectPrototype, meteor.Damage, comp.TileDamageDelay);
                _audio.PlayPvs(comp.MeteorRainSound, uid);

                comp.ActiveMeteors.RemoveAt(i);
            }
        }
    }

    private void ProcessPendingTileDamage(EntityUid uid)
    {
        if (!_pendingDamage.TryGetValue(uid, out var pending) || pending.Count == 0)
            return;

        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var hit = pending[i];
            if (_timing.CurTime < hit.TriggerTime)
                continue;

            if (hit.SpawnEffectOnTrigger)
            {
                var fireUid = Spawn(hit.EffectProto, hit.Tile);
                var fc = EnsureComp<AshDrakeFireTileComponent>(fireUid);
                fc.DrakeUid = uid;
                fc.Damage = hit.Damage;
                fc.NextDamageTime = _timing.CurTime; // first check immediately
            }
            else
            {
                DamageEntitiesOnTile(uid, hit.Tile, hit.Damage);
            }
            pending.RemoveAt(i);
        }
    }

    private void SpawnDamageTile(EntityUid uid, EntityCoordinates tile, string effectProto, float damage, float delay)
    {
        var fireUid = Spawn(effectProto, tile);
        var fc = EnsureComp<AshDrakeFireTileComponent>(fireUid);
        fc.DrakeUid = uid;
        fc.Damage = damage;
        fc.NextDamageTime = _timing.CurTime + TimeSpan.FromSeconds(delay);
    }

    private void SpawnSnakeRay(
        EntityUid uid,
        EntityCoordinates origin,
        Vector2 direction,
        int range,
        AshDrakeComponent comp,
        float damage,
        float baseDelay,
        float healthRatio = 0f)
    {
        // At full HP (healthRatio=0): 2x slower than base; faster as HP decreases
        var stepDelay = comp.FireConeStepDelay * 2f * (1f - healthRatio * 0.67f);
        stepDelay = MathF.Max(stepDelay, 0.03f);

        for (var step = 1; step <= range; step++)
        {
            var tile = SnapToTile(origin.Offset(direction * step));
            var delay = baseDelay + step * stepDelay;
            QueueDelayedDamageTile(uid, tile, comp.SnakeFirePrototype, damage, delay);
        }
    }

    private void QueueDelayedDamageTile(EntityUid uid, EntityCoordinates tile, string effectProto, float damage, float delay)
    {
        if (!_pendingDamage.TryGetValue(uid, out var pending))
        {
            pending = new List<PendingTileDamage>();
            _pendingDamage[uid] = pending;
        }

        pending.Add(new PendingTileDamage(
            tile,
            _timing.CurTime + TimeSpan.FromSeconds(delay),
            damage,
            effectProto,
            true));
    }

    private void DamageEntitiesOnTile(EntityUid uid, EntityCoordinates tile, float damage)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict.Add("Heat", FixedPoint2.New(damage));

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } candidate)
                continue;

            if (!Exists(candidate))
                continue;

            if (!TryComp<MobStateComponent>(candidate, out var state) || state.CurrentState != MobState.Alive)
                continue;

            if (!TryComp<DamageableComponent>(candidate, out var damageable))
                continue;

            if (!IsSameTile(tile, SnapToTile(Transform(candidate).Coordinates)))
                continue;

            _damageable.TryChangeDamage((candidate, damageable), spec, origin: uid);
        }
    }

    private static EntityCoordinates SnapToTile(EntityCoordinates coords)
        => new(coords.EntityId, MathF.Round(coords.X), MathF.Round(coords.Y));

    private static bool IsSameTile(EntityCoordinates a, EntityCoordinates b)
        => a.EntityId == b.EntityId
           && MathF.Round(a.X) == MathF.Round(b.X)
           && MathF.Round(a.Y) == MathF.Round(b.Y);

    private static EntityCoordinates LerpTile(EntityCoordinates start, EntityCoordinates end, float t)
    {
        var x = start.X + (end.X - start.X) * t;
        var y = start.Y + (end.Y - start.Y) * t;
        return new EntityCoordinates(start.EntityId, MathF.Round(x), MathF.Round(y));
    }

    // Smooth lerp for visual-only entities (shadow) — no tile rounding
    private static EntityCoordinates LerpSmooth(EntityCoordinates start, EntityCoordinates end, float t)
    {
        var x = start.X + (end.X - start.X) * t;
        var y = start.Y + (end.Y - start.Y) * t;
        return new EntityCoordinates(start.EntityId, x, y);
    }
}
