using System.Linq;
using Content.Server.Imperial.Cult.Components;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Cult;

/// <summary>Мастер культа (datum/antagonist/cult/master, cult_comms.dm).</summary>
public sealed partial class CultSystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly Content.Server.Pinpointer.NavMapSystem _navMap = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private const float MarkDuration = 90f;

    private void InitializeLeader()
    {
        SubscribeLocalEvent<CultistComponent, CultPassMantleActionEvent>(OnPassMantle);
        SubscribeLocalEvent<CultistComponent, CultFinalReckoningActionEvent>(OnFinalReckoning);
        SubscribeLocalEvent<CultistComponent, CultReckoningDoAfterEvent>(OnReckoningDoAfter);
        SubscribeLocalEvent<CultistComponent, CultMarkTargetActionEvent>(OnMarkTarget);
        SubscribeLocalEvent<CultistComponent, CultEldritchPulseActionEvent>(OnEldritchPulse);
        SubscribeLocalEvent<CultBloodTargetComponent, EntityTerminatingEvent>(OnBloodTargetTerminating);
    }

    /// <summary>Есть ли сейчас мастер культа.</summary>
    public EntityUid? GetLeader()
    {
        var query = EntityQueryEnumerator<CultistComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Leader)
                return uid;
        }
        return null;
    }

    /// <summary>datum/antagonist/cult/master/on_gain().</summary>
    public void MakeLeader(EntityUid uid, bool announce = true)
    {
        if (!TryComp<CultistComponent>(uid, out var cultist) || cultist.Leader)
            return;

        cultist.Leader = true;
        Dirty(uid, cultist);

        var rule = _rule.EnsureRule();
        if (_mind.TryGetMind(uid, out var mindId, out _))
            rule.Comp.LeaderMind = mindId;

        if (!rule.Comp.ReckoningComplete)
            AddMasterAction(uid, cultist, ReckoningAction);
        AddMasterAction(uid, cultist, MarkAction);
        AddMasterAction(uid, cultist, PulseAction);
        if (!rule.Comp.LeaderPassedOn)
            AddMasterAction(uid, cultist, PassMantleAction);

        if (!announce)
            return;
        foreach (var member in CultistBodies())
        {
            if (member != uid)
                Message(member, Loc.GetString("cult-master-announce", ("name", Name(uid))));
        }
        Message(uid, Loc.GetString("cult-master-greet"));
    }

    private void AddMasterAction(EntityUid uid, CultistComponent cultist, string proto)
    {
        EntityUid? action = null;
        if (_actions.AddAction(uid, ref action, proto) && action != null)
            cultist.MasterActions.Add(action.Value);
    }

    public void DemoteLeader(EntityUid uid, CultistComponent? cultist = null)
    {
        if (!Resolve(uid, ref cultist, false) || !cultist.Leader)
            return;
        cultist.Leader = false;
        foreach (var action in cultist.MasterActions)
            _actions.RemoveAction(uid, action);
        cultist.MasterActions.Clear();
        Dirty(uid, cultist);
    }

    private void RemoveMasterAction(EntityUid uid, CultistComponent cultist, string proto)
    {
        foreach (var action in cultist.MasterActions.ToList())
        {
            if (MetaData(action).EntityPrototype?.ID != proto)
                continue;
            _actions.RemoveAction(uid, action);
            cultist.MasterActions.Remove(action);
        }
    }

    /// <summary>Смерть мастера: все культисты слышат далёкий грохот.</summary>
    private void Deathrattle(EntityUid leader)
    {
        if (EntityQuery<CultNarSieComponent>().Any())
            return;
        var where = _navMap.GetNearestBeaconString(leader, onlyName: true);
        foreach (var member in CultistBodies())
        {
            if (member == leader)
                continue;
            SoundTo(member, CultSounds.VeryFarNoise);
            Message(member, Loc.GetString("cult-master-died", ("name", Name(leader)), ("area", where)));
        }
    }

    #region Pass the Mantle

    private void OnPassMantle(Entity<CultistComponent> ent, ref CultPassMantleActionEvent args)
    {
        args.Handled = true;
        if (!ent.Comp.Leader)
            return;

        var options = new List<CultMenuOption>();
        foreach (var member in CultistBodies())
        {
            if (member == ent.Owner || _mobState.IsDead(member) || !HasComp<ActorComponent>(member)
                || !HasComp<Content.Shared.Humanoid.HumanoidProfileComponent>(member))
                continue;
            options.Add(new CultMenuOption(GetNetEntity(member).ToString(), Name(member)));
        }

        if (options.Count == 0)
        {
            Message(ent, Loc.GetString("cult-mantle-nobody"));
            return;
        }

        var user = ent.Owner;
        OpenChoice(user, Loc.GetString("cult-mantle-title"), options, id =>
        {
            if (!NetEntity.TryParse(id, out var net) || !TryGetEntity(net, out var target))
                return;
            if (!TryComp<CultistComponent>(user, out var comp) || !comp.Leader || !IsCultist(target.Value) || _mobState.IsDead(target.Value))
                return;

            var rule = _rule.EnsureRule();
            rule.Comp.LeaderPassedOn = true;
            DemoteLeader(user, comp);
            MakeLeader(target.Value, announce: false);
            if (TryComp<CultistComponent>(target, out var newLeader))
                RemoveMasterAction(target.Value, newLeader, PassMantleAction);
            Message(target.Value, Loc.GetString("cult-mantle-received", ("name", Name(user))));
            MessageCult(Loc.GetString("cult-mantle-passed", ("old", Name(user)), ("new", Name(target.Value))));
        }, radial: false);
    }

    #endregion

    #region Final Reckoning

    private void OnFinalReckoning(Entity<CultistComponent> ent, ref CultFinalReckoningActionEvent args)
    {
        args.Handled = true;
        if (!ent.Comp.Leader)
            return;
        if (_rule.IsInSummonSpot(ent))
        {
            Message(ent, Loc.GetString("cult-reckoning-veil-weak"));
            return;
        }
        ReckoningStage(ent, 1);
    }

    private void ReckoningStage(EntityUid user, int stage)
    {
        switch (stage)
        {
            case 1:
                Say(user, "C'arta forbici!");
                break;
            case 2:
                Say(user, "Pleggh e'ntrath!");
                _audio.PlayPvs(CultSounds.NarsieAttack, user, Robust.Shared.Audio.AudioParams.Default.WithVolume(Db(50)));
                break;
            case 3:
                Say(user, "Barhah hra zar'garis!");
                _audio.PlayPvs(CultSounds.NarsieAttack, user, Robust.Shared.Audio.AudioParams.Default.WithVolume(Db(75)));
                break;
            case 4:
                Say(user, "N'ath reth sh'yro eth d'rekkathnor!!!");
                _audio.PlayPvs(CultSounds.NarsieAttack, user);
                break;
        }

        if (FreeTurfsAround(user).Count == 0)
        {
            Message(user, Loc.GetString("cult-reckoning-no-space"));
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(3), new CultReckoningDoAfterEvent { Stage = stage }, user)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnReckoningDoAfter(Entity<CultistComponent> ent, ref CultReckoningDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Leader)
            return;
        args.Handled = true;

        var stage = args.Stage;
        var destinations = FreeTurfsAround(ent);
        if (destinations.Count == 0)
        {
            Message(ent, Loc.GetString("cult-reckoning-no-space"));
            return;
        }

        foreach (var member in CultistBodies().ToList())
        {
            if (_mobState.IsDead(member))
                continue;
            var coords = Transform(member).Coordinates;
            switch (stage)
            {
                case 1:
                    Effect("CultEffectSparks", coords);
                    _audio.PlayPvs(CultSounds.Sparks, coords, Robust.Shared.Audio.AudioParams.Default.WithVolume(Db(50)));
                    break;
                case 2:
                    Effect("CultEffectPhaseOut", coords, Transform(member).LocalRotation);
                    _audio.PlayPvs(CultSounds.Sparks, coords, Robust.Shared.Audio.AudioParams.Default.WithVolume(Db(75)));
                    break;
                case 3:
                    Effect("CultEffectPhaseIn", coords, Transform(member).LocalRotation);
                    _audio.PlayPvs(CultSounds.Sparks, coords);
                    break;
                case 4:
                    _audio.PlayPvs(CultSounds.ExitBlood, coords);
                    if (member == ent.Owner)
                        break;
                    var final = _random.Pick(destinations);
                    Effect("CultEffectBloodIn", final);
                    var target = member;
                    Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(1), () =>
                    {
                        if (TerminatingOrDeleted(target))
                            return;
                        Effect("CultEffectBloodOut", Transform(target).Coordinates);
                        _transform.SetCoordinates(target, final);
                        _transform.AttachToGridOrMap(target);
                    });
                    break;
            }
        }

        if (stage < 4)
        {
            ReckoningStage(ent, stage + 1);
            return;
        }

        var rule = _rule.EnsureRule();
        rule.Comp.ReckoningComplete = true;
        RemoveMasterAction(ent, ent.Comp, ReckoningAction);
    }

    /// <summary>orange(1, owner) без непроходимых турфов.</summary>
    public List<EntityCoordinates> FreeTurfsAround(EntityUid uid, int range = 1)
    {
        var result = new List<EntityCoordinates>();
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return result;

        var center = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                if (x == 0 && y == 0)
                    continue;
                var idx = center + new Vector2i(x, y);
                if (!_map.TryGetTileRef(grid, gridComp, idx, out var tile) || tile.Tile.IsEmpty)
                    continue;
                if (_turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                    continue;
                result.Add(_map.GridTileToLocal(grid, gridComp, idx));
            }
        }
        return result;
    }

    #endregion

    #region Mark Target

    private void OnMarkTarget(Entity<CultistComponent> ent, ref CultMarkTargetActionEvent args)
    {
        if (!ent.Comp.Leader)
            return;

        var target = args.Entity;
        if (target == null || target == ent.Owner)
            return;

        var rule = _rule.EnsureRule();
        if (rule.Comp.BloodTarget != null)
        {
            Message(ent, Loc.GetString("cult-mark-already"));
            return;
        }

        if (SetBloodTarget(target.Value, ent, TimeSpan.FromSeconds(MarkDuration)))
            args.Handled = true;
    }

    /// <summary>datum/team/cult/set_blood_target().</summary>
    public bool SetBloodTarget(EntityUid target, EntityUid? marker, TimeSpan? duration)
    {
        if (TerminatingOrDeleted(target))
            return false;

        var rule = _rule.EnsureRule();
        UnsetBloodTarget(silent: true);

        rule.Comp.BloodTarget = target;
        rule.Comp.BloodTargetEnd = duration == null ? null : _timing.CurTime + duration.Value;
        EnsureComp<CultBloodTargetComponent>(target);

        var where = _navMap.GetNearestBeaconString(target, onlyName: true);
        var message = marker != null
            ? Loc.GetString("cult-mark-set", ("marker", Name(marker.Value)), ("target", Name(target)), ("area", where))
            : Loc.GetString("cult-mark-set-narsie", ("target", Name(target)), ("area", where));

        foreach (var member in CultistBodies())
        {
            if (_mobState.IsDead(member))
                continue;
            SoundTo(member, CultSounds.OverHere, Db(75));
            Message(member, message);
        }

        return true;
    }

    public void UnsetBloodTarget(bool silent = false)
    {
        if (!_rule.TryGetRule(out var rule) || rule.Value.Comp.BloodTarget is not { } target)
            return;

        var lost = TerminatingOrDeleted(target);
        rule.Value.Comp.BloodTarget = null;
        rule.Value.Comp.BloodTargetEnd = null;
        if (!lost)
            RemComp<CultBloodTargetComponent>(target);

        if (silent)
            return;

        foreach (var member in CultistBodies())
            Message(member, Loc.GetString(lost ? "cult-mark-lost" : "cult-mark-expired"));
    }

    private void OnBloodTargetTerminating(Entity<CultBloodTargetComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_rule.TryGetRule(out var rule) && rule.Value.Comp.BloodTarget == ent.Owner)
            UnsetBloodTarget();
    }

    private void UpdateBloodTarget(TimeSpan now)
    {
        if (!_rule.TryGetRule(out var rule))
            return;
        if (rule.Value.Comp.BloodTargetEnd is { } end && now >= end)
            UnsetBloodTarget();
    }

    #endregion

    #region Eldritch Pulse

    private void OnEldritchPulse(Entity<CultistComponent> ent, ref CultEldritchPulseActionEvent args)
    {
        if (!ent.Comp.Leader)
            return;

        var holder = EnsureComp<CultPulseHolderComponent>(ent);
        var user = ent.Owner;
        var userCoords = _transform.GetMapCoordinates(user);
        var targetCoords = _transform.ToMapCoordinates(args.Target);
        if (targetCoords.MapId != userCoords.MapId || (targetCoords.Position - userCoords.Position).Length() > 7.5f)
            return;

        // Первый клик: выбрать культиста или постройку культа.
        if (holder.Seized == null || TerminatingOrDeleted(holder.Seized.Value))
        {
            if (args.Entity is not { } picked || !(IsCultAligned(picked) || HasComp<CultStructureComponent>(picked)))
            {
                Message(user, Loc.GetString("cult-pulse-invalid"));
                return;
            }

            holder.Seized = picked;
            var mark = EnsureComp<CultPulseSelectedComponent>(picked);
            mark.Master = user;
            Dirty(picked, mark);
            Message(user, Loc.GetString("cult-pulse-selected", ("target", picked)));
            return;
        }

        var seized = holder.Seized.Value;
        holder.Seized = null;
        RemComp<CultPulseSelectedComponent>(seized);

        var from = _transform.GetMapCoordinates(seized);
        if (from.MapId != targetCoords.MapId || (from.Position - targetCoords.Position).Length() >= 16f)
        {
            Message(user, Loc.GetString("cult-pulse-too-far"));
            return;
        }

        var dest = _transform.ToCoordinates(targetCoords);
        var destTile = _turf.GetTileRef(dest);
        if (destTile == null || destTile.Value.Tile.IsEmpty || _turf.IsTileBlocked(destTile.Value, CollisionGroup.Impassable) || IsBlessed(dest))
        {
            Message(user, Loc.GetString("cult-pulse-blocked"));
            return;
        }

        var prev = Transform(seized).Coordinates;
        Effect("CultEffectSparks", prev);
        _transform.SetMapCoordinates(seized, targetCoords);
        _transform.AttachToGridOrMap(seized);
        Effect("CultEffectSparks", Transform(seized).Coordinates);
        _audio.PlayPvs(CultSounds.ExitBlood, Transform(seized).Coordinates, Robust.Shared.Audio.AudioParams.Default.WithVolume(Db(50)));
        Beam(user, Transform(seized).Coordinates, "CultBeamSend", 0.4f);
        Beam(user, prev, "CultBeamSend", 0.4f);
        Message(user, Loc.GetString("cult-pulse-success", ("target", seized)));
        args.Handled = true;
    }

    #endregion

    /// <summary>atom/Beam(): растянутый спрайт от источника до точки на время.</summary>
    public void Beam(EntityUid from, EntityCoordinates to, string proto, float seconds)
    {
        var fromPos = _transform.GetWorldPosition(from);
        var toMap = _transform.ToMapCoordinates(to);
        if (toMap.MapId != Transform(from).MapID)
            return;
        var delta = toMap.Position - fromPos;
        var length = delta.Length();
        if (length < 0.01f)
            return;

        var mid = new MapCoordinates(fromPos + delta / 2f, toMap.MapId);
        var beam = Spawn(proto, mid);
        _transform.SetWorldRotation(beam, delta.ToWorldAngle());
        var comp = EnsureComp<CultBeamComponent>(beam);
        comp.Length = length;
        Dirty(beam, comp);
        EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(beam).Lifetime = seconds;
    }
}
