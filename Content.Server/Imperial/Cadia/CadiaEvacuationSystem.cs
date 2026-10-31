using System.Linq;
using System.Numerics;
using Content.Server.Ghost;
using Content.Server.Popups;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Падение Кадии: когда Хаос взял пилоны или цитадель, транспорты садятся у маяка и увозят всех, кого успели донести.
/// Раненые грузятся первыми, поэтому в последние минуты всё решают медики и те, кто держит площадку.
/// </summary>
public sealed class CadiaEvacuationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;

    private const string ShipProto = "CadiaEvacShip";
    private static readonly TimeSpan GroundTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ClimbTime = TimeSpan.FromSeconds(4);
    private static readonly SoundSpecifier ShipSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg");
    private static readonly SoundSpecifier LeaveSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg");
    private static readonly SoundSpecifier ShipComingSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    private readonly HashSet<Entity<CadiaFactionMemberComponent>> _inRange = new();
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaEvacBeaconComponent, BeforeDamageChangedEvent>(OnBeaconBeforeDamage);
    }

    private void OnBeaconBeforeDamage(Entity<CadiaEvacBeaconComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!_rule.TryGetActiveWar(out var war) || war.Phase != CadiaWarPhase.Evacuation)
            args.Cancelled = true;
    }

    public bool HasBeacon()
    {
        var query = EntityQueryEnumerator<CadiaEvacBeaconComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid))
                return true;
        }
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var ships = EntityQueryEnumerator<CadiaEvacShipComponent>();
        while (ships.MoveNext(out var shipUid, out var ship))
        {
            if (now >= ship.GoneAt)
                QueueDel(shipUid);
        }

        if (now < _next)
            return;
        _next = now + TimeSpan.FromSeconds(1);
        if (!_rule.TryGetActiveRule(out var rule, out var war) || war.Phase != CadiaWarPhase.Evacuation)
            return;

        var anyBeacon = false;
        TimeSpan? soonest = null;
        var beacons = EntityQueryEnumerator<CadiaEvacBeaconComponent, TransformComponent>();
        while (beacons.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (TerminatingOrDeleted(uid))
                continue;
            anyBeacon = true;
            beacon.NextLoad ??= now + beacon.Interval;
            var load = beacon.NextLoad.Value;

            if (!beacon.ShipComing && now >= load - beacon.Warning)
            {
                beacon.ShipComing = true;
                CallShip((uid, beacon), load, now);
            }
            if (now >= load)
            {
                beacon.ShipComing = false;
                beacon.NextLoad = load + beacon.Interval;
                Load((uid, beacon), xform, war);
            }
            if (soonest == null || beacon.NextLoad < soonest)
                soonest = beacon.NextLoad;
        }

        if (soonest is { } next && next != war.NextTransport)
        {
            war.NextTransport = next;
            _rule.DirtyWar();
        }

        // Маяк уничтожен или спасать больше некого: Кадия пала окончательно.
        if (!anyBeacon || !AnyDefenderAlive())
            _rule.FinishFall(rule);
    }

    private bool AnyDefenderAlive()
    {
        var query = EntityQueryEnumerator<CadiaFactionMemberComponent>();
        while (query.MoveNext(out var uid, out var member))
        {
            if (member.Role != null && member.Faction == CadiaFaction.Imperium && !_mobState.IsDead(uid))
                return true;
        }
        return false;
    }

    private void CallShip(Entity<CadiaEvacBeaconComponent> beacon, TimeSpan landAt, TimeSpan now)
    {
        var shipEnt = Spawn(ShipProto, _xform.GetMapCoordinates(beacon));
        var ship = EnsureComp<CadiaEvacShipComponent>(shipEnt);
        ship.Start = now;
        ship.LandAt = landAt;
        ship.LeaveAt = landAt + GroundTime;
        ship.GoneAt = ship.LeaveAt + ClimbTime;
        Dirty(shipEnt, ship);
        _audio.PlayPvs(ShipSound, shipEnt);

        _rule.Announce(CadiaFaction.Imperium, Loc.GetString("cadia-evac-ship-coming", ("seconds", (int) (landAt - now).TotalSeconds)), ShipComingSound);

        // Каждому защитнику: куда бежать. Маяк может быть далеко за краем экрана.
        var from = _xform.GetMapCoordinates(beacon);
        var query = EntityQueryEnumerator<CadiaFactionMemberComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var member, out var xform))
        {
            if (member.Faction != CadiaFaction.Imperium || member.Role == null || xform.MapID != from.MapId)
                continue;
            var delta = from.Position - _xform.GetWorldPosition(xform);
            var dir = delta.ToWorldAngle().GetDir().ToString().ToLowerInvariant();
            _popup.PopupEntity(Loc.GetString("cadia-evac-direction", ("dir", Loc.GetString($"cadia-dir-{dir}")), ("meters", (int) delta.Length())), uid, uid, PopupType.Medium);
        }
    }

    private void Load(Entity<CadiaEvacBeaconComponent> beacon, TransformComponent xform, CadiaWarStateComponent war)
    {
        _inRange.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, beacon.Comp.Radius, _inRange);

        // Сначала те, кто сам не дойдёт: раненых несли сюда не для того, чтобы оставить на бетоне.
        var boarding = _inRange
            .Where(m => m.Comp.Role != null && m.Comp.Faction == CadiaFaction.Imperium && !_mobState.IsDead(m))
            .OrderByDescending(m => _mobState.IsCritical(m))
            .Take(beacon.Comp.Capacity)
            .ToList();

        foreach (var soldier in boarding)
            Evacuate(soldier);

        _audio.PlayPvs(LeaveSound, beacon);
        if (boarding.Count == 0)
        {
            _rule.Announce(CadiaFaction.Imperium, Loc.GetString("cadia-evac-empty"), ShipComingSound);
            return;
        }

        war.EvacSaved += boarding.Count;
        _rule.DirtyWar();
        _rule.Feed(Loc.GetString("cadia-feed-evac", ("count", boarding.Count), ("total", war.EvacSaved)), CadiaFaction.Imperium);
        var text = Loc.GetString("cadia-evac-loaded", ("count", boarding.Count), ("total", war.EvacSaved));
        _rule.Announce(CadiaFaction.Imperium, text, ShipComingSound);
        _rule.Announce(CadiaFaction.Chaos, Loc.GetString("cadia-evac-loaded-chaos", ("count", boarding.Count)), ShipComingSound);
    }

    /// <summary>Боец улетел: игрок становится наблюдателем, тело со снаряжением убирается с поля.</summary>
    private void Evacuate(EntityUid soldier)
    {
        if (_mind.TryGetMind(soldier, out var mindId, out var mind))
            _ghost.OnGhostAttempt(mindId, false, true, true, mind);
        QueueDel(soldier);
    }
}
