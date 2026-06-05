using System.Numerics;
using Content.Server.Actions;
using Content.Server.Imperial.Lavaland.AshDrake;
using Content.Shared.Imperial.Lavaland.DrakePlayerActions;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.DrakePlayerActions;

/// <summary>
/// Handles fire-cone and flight actions for player-controlled drake mobs
/// (MobLesserDragonPlayer, MobDragonidPlayer).
/// </summary>
public sealed class DrakePlayerActionsSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier FireConeSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_fireball.ogg");
    private static readonly SoundSpecifier LandingSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_misc_demon_attack1.ogg");
    private static readonly EntProtoId RevertPolymorphAction = "ActionRevertPolymorph";

    // Pending fire tiles: casterUid → list of (tileCoords, triggerTime)
    private readonly Dictionary<EntityUid, List<(EntityCoordinates Tile, TimeSpan TriggerTime, float Damage)>>
        _pendingTiles = new();

    private readonly Dictionary<EntityUid, PendingFlight> _pendingFlights = new();

    private readonly record struct PendingFlight(
        EntityCoordinates Destination,
        TimeSpan LandTime,
        EntityUid WarningUid,
        DrakeFlightComponent Component);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DrakeFireConeComponent, MapInitEvent>(OnFireConeInit);
        SubscribeLocalEvent<DrakeFireConeComponent, ComponentShutdown>(OnFireConeShutdown);
        SubscribeLocalEvent<DrakeFireConeComponent, DrakeFireConeEvent>(OnFireCone);

        SubscribeLocalEvent<DrakeFlightComponent, MapInitEvent>(OnFlightInit);
        SubscribeLocalEvent<DrakeFlightComponent, ComponentShutdown>(OnFlightShutdown);
        SubscribeLocalEvent<DrakeFlightComponent, DrakeFlightEvent>(OnFlight);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessPendingTiles();
        ProcessPendingFlights();
    }

    // ── Fire cone action ─────────────────────────────────────────────────────

    private void OnFireConeInit(EntityUid uid, DrakeFireConeComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.ActionEntity, comp.ActionId);
    }

    private void OnFireConeShutdown(EntityUid uid, DrakeFireConeComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.ActionEntity);
    }

    private void OnFireCone(EntityUid uid, DrakeFireConeComponent comp, DrakeFireConeEvent args)
    {
        if (args.Handled)
            return;

        var origin = SnapToTile(Transform(uid).Coordinates);
        var target = _xform.WithEntityId(args.Target, origin.EntityId);

        var dx = target.X - origin.X;
        var dy = target.Y - origin.Y;

        if (dx == 0f && dy == 0f)
            return;

        var baseAngle = MathF.Atan2(dy, dx);

        var halfSpread = comp.SpreadDeg * (MathF.PI / 180f) / 2f;

        for (var i = 0; i < comp.RayCount; i++)
        {
            var t = comp.RayCount <= 1 ? 0f : (float)i / (comp.RayCount - 1) - 0.5f;
            var rayAngle = baseAngle + t * 2f * halfSpread;
            var dir = new Vector2(MathF.Cos(rayAngle), MathF.Sin(rayAngle));
            QueueFireRay(uid, origin, dir, comp.Range, comp.StepDelay, comp.TileDamage);
        }

        _audio.PlayPvs(FireConeSound, uid);

        args.Handled = true;
    }

    // ── Flight action ────────────────────────────────────────────────────────

    private void OnFlightInit(EntityUid uid, DrakeFlightComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.ActionEntity, comp.ActionId);
        _actions.AddAction(uid, ref comp.RevertActionEntity, RevertPolymorphAction);
    }

    private void OnFlightShutdown(EntityUid uid, DrakeFlightComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.ActionEntity);
        _actions.RemoveAction(uid, comp.RevertActionEntity);
    }

    private void OnFlight(EntityUid uid, DrakeFlightComponent comp, DrakeFlightEvent args)
    {
        if (args.Handled || _pendingFlights.ContainsKey(uid))
            return;

        var originXform = Transform(uid);
        var originMap = _xform.ToMapCoordinates(originXform.Coordinates);
        var originPos = originMap.Position;

        // Clamp destination to max range
        var targetMapCoords = _xform.ToMapCoordinates(args.Target);

        if (targetMapCoords.MapId != originMap.MapId)
            return;

        var delta = targetMapCoords.Position - originPos;

        if (delta == Vector2.Zero)
            return;

        if (delta.Length() > comp.MaxRange)
            delta = Vector2.Normalize(delta) * comp.MaxRange;

        var destPos = originPos + delta;
        var destCoords = _xform.ToCoordinates(new MapCoordinates(destPos, originMap.MapId));

        var warningUid = Spawn(comp.LandingProto, destCoords);
        _pendingFlights[uid] = new PendingFlight(
            destCoords,
            _timing.CurTime + TimeSpan.FromSeconds(comp.FlightDuration),
            warningUid,
            comp);

        _audio.PlayPvs(FireConeSound, uid);

        args.Handled = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void QueueFireRay(EntityUid caster, EntityCoordinates origin, Vector2 dir, int range, float stepDelay, float damage)
    {
        if (!_pendingTiles.TryGetValue(caster, out var list))
        {
            list = new List<(EntityCoordinates, TimeSpan, float)>();
            _pendingTiles[caster] = list;
        }

        for (var step = 1; step <= range; step++)
        {
            var tile = SnapToTile(origin.Offset(dir * step));
            var triggerTime = _timing.CurTime + TimeSpan.FromSeconds(step * stepDelay);
            list.Add((tile, triggerTime, damage));
        }
    }

    private void ProcessPendingTiles()
    {
        var now = _timing.CurTime;
        var toClean = new List<EntityUid>();

        foreach (var (casterUid, pending) in _pendingTiles)
        {
            if (!Exists(casterUid))
            {
                toClean.Add(casterUid);
                continue;
            }

            // Remove and spawn tiles whose time has arrived
            pending.RemoveAll(entry =>
            {
                if (now < entry.TriggerTime)
                    return false;

                var tileEnt = Spawn("ImperialAshDrakeSnakeFireTile", entry.Tile);
                var fireTile = EnsureComp<AshDrakeFireTileComponent>(tileEnt);
                fireTile.DrakeUid = casterUid;
                fireTile.Damage = entry.Damage;
                fireTile.NextDamageTime = now;
                return true;
            });

            if (pending.Count == 0)
                toClean.Add(casterUid);
        }

        foreach (var uid in toClean)
            _pendingTiles.Remove(uid);
    }

    private void ProcessPendingFlights()
    {
        if (_pendingFlights.Count == 0)
            return;

        var now = _timing.CurTime;
        var completed = new List<EntityUid>();

        foreach (var (uid, flight) in _pendingFlights)
        {
            if (!Exists(uid))
            {
                if (flight.WarningUid.Valid && Exists(flight.WarningUid))
                    Del(flight.WarningUid);
                completed.Add(uid);
                continue;
            }

            if (now < flight.LandTime)
                continue;

            if (flight.WarningUid.Valid && Exists(flight.WarningUid))
                Del(flight.WarningUid);

            _xform.SetCoordinates(uid, flight.Destination);
            _audio.PlayPvs(LandingSound, uid);

            var radius = flight.Component.LandingFireRadius;
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y > radius * radius)
                        continue;

                    if (x == 0 && y == 0)
                        continue;

                    var tile = SnapToTile(flight.Destination.Offset(new Vector2(x, y)));
                    var fireUid = Spawn(flight.Component.LandingFireProto, tile);
                    var fireTile = EnsureComp<AshDrakeFireTileComponent>(fireUid);
                    fireTile.DrakeUid = uid;
                    fireTile.Damage = flight.Component.LandingFireDamage;
                    fireTile.NextDamageTime = now;
                }
            }

            completed.Add(uid);
        }

        foreach (var uid in completed)
        {
            _pendingFlights.Remove(uid);
        }
    }

    private static EntityCoordinates SnapToTile(EntityCoordinates coords)
        => new(coords.EntityId, MathF.Round(coords.X), MathF.Round(coords.Y));
}
