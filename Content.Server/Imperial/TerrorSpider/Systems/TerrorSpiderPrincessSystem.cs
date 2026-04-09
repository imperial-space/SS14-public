using Content.Server.Actions;
using Content.Server.Chat.Managers;
using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Tag;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Imperial.TerrorSpider.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderPrincessSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> _terrorSpiderTag = "TerrorSpider";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly MovementModStatusSystem _movementModStatus = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderPrincessComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderPrincessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderPrincessComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TerrorSpiderPrincessComponent, TerrorSpiderMotherRemoteViewNextActionEvent>(OnRemoteViewNextAction);
        SubscribeLocalEvent<TerrorSpiderPrincessComponent, TerrorSpiderMotherRemoteViewPreviousActionEvent>(OnRemoteViewPreviousAction);
        SubscribeLocalEvent<TerrorSpiderPrincessComponent, TerrorSpiderMotherRemoteViewExitActionEvent>(OnRemoteViewExitAction);
        SubscribeLocalEvent<TerrorSpiderPrincessComponent, TerrorSpiderPrincessHiveSenseActionEvent>(OnHiveSenseAction);
        SubscribeLocalEvent<TerrorSpiderPrincessComponent, TerrorSpiderPrincessScreamActionEvent>(OnScreamAction);
        SubscribeLocalEvent<TerrorSpiderPrincessComponent, TerrorSpiderPrincessLayEggActionEvent>(OnLayEggAction);
        SubscribeLocalEvent<TerrorSpiderPrincessEggComponent, MapInitEvent>(OnEggMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateRemoteViewMovementLock();
        UpdateEggs(now);
        UpdateOrphanDamage(now);
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderPrincessComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.RemoteViewNextActionEntity, comp.RemoteViewNextAction);
        _actions.AddAction(uid, ref comp.RemoteViewPreviousActionEntity, comp.RemoteViewPreviousAction);
        _actions.AddAction(uid, ref comp.RemoteViewExitActionEntity, comp.RemoteViewExitAction);
        _actions.AddAction(uid, ref comp.HiveSenseActionEntity, comp.HiveSenseAction);
        _actions.AddAction(uid, ref comp.ScreamActionEntity, comp.ScreamAction);
        _actions.AddAction(uid, ref comp.LayEggRusarActionEntity, comp.LayEggRusarAction);
        _actions.AddAction(uid, ref comp.LayEggDronActionEntity, comp.LayEggDronAction);
        _actions.AddAction(uid, ref comp.LayEggLurkerActionEntity, comp.LayEggLurkerAction);
        _actions.AddAction(uid, ref comp.LayEggHealerActionEntity, comp.LayEggHealerAction);
        _actions.AddAction(uid, ref comp.LayEggReaperActionEntity, comp.LayEggReaperAction);
        _actions.AddAction(uid, ref comp.LayEggWidowActionEntity, comp.LayEggWidowAction);
        _actions.AddAction(uid, ref comp.LayEggGuardianActionEntity, comp.LayEggGuardianAction);
        _actions.AddAction(uid, ref comp.LayEggDestroyerActionEntity, comp.LayEggDestroyerAction);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderPrincessComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.RemoteViewNextActionEntity);
        _actions.RemoveAction(uid, comp.RemoteViewPreviousActionEntity);
        _actions.RemoveAction(uid, comp.RemoteViewExitActionEntity);
        _actions.RemoveAction(uid, comp.HiveSenseActionEntity);
        _actions.RemoveAction(uid, comp.ScreamActionEntity);
        _actions.RemoveAction(uid, comp.LayEggRusarActionEntity);
        _actions.RemoveAction(uid, comp.LayEggDronActionEntity);
        _actions.RemoveAction(uid, comp.LayEggLurkerActionEntity);
        _actions.RemoveAction(uid, comp.LayEggHealerActionEntity);
        _actions.RemoveAction(uid, comp.LayEggReaperActionEntity);
        _actions.RemoveAction(uid, comp.LayEggWidowActionEntity);
        _actions.RemoveAction(uid, comp.LayEggGuardianActionEntity);
        _actions.RemoveAction(uid, comp.LayEggDestroyerActionEntity);
    }

    private void OnMobStateChanged(Entity<TerrorSpiderPrincessComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var broodQuery = EntityQueryEnumerator<TerrorSpiderPrincessBroodComponent>();
        while (broodQuery.MoveNext(out var uid, out var brood))
        {
            if (brood.Princess != ent.Owner)
                continue;

            var orphan = EnsureComp<TerrorSpiderPrincessOrphanDamageComponent>(uid);
            orphan.DamagePerTick = ent.Comp.OrphanDamagePerTick;
            orphan.TickInterval = ent.Comp.OrphanDamageInterval;
            orphan.NextTick = TimeSpan.Zero;
        }
    }

    private void OnEggMapInit(Entity<TerrorSpiderPrincessEggComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.HatchAt != TimeSpan.Zero)
            return;

        ent.Comp.HatchAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.HatchDelay);
    }

    private void OnRemoteViewNextAction(Entity<TerrorSpiderPrincessComponent> ent, ref TerrorSpiderMotherRemoteViewNextActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EyeComponent>(ent.Owner, out _))
            return;

        var candidates = GetRemoteViewCandidates(ent.Owner);

        if (candidates.Count == 0)
        {
            _eye.SetTarget(ent.Owner, null);
            ent.Comp.RemoteViewIndex = -1;
            UpdateRemoteViewMovementLock(ent, null);
            args.Handled = true;
            return;
        }

        if (ent.Comp.RemoteViewIndex < -1 || ent.Comp.RemoteViewIndex >= candidates.Count)
            ent.Comp.RemoteViewIndex = -1;

        ent.Comp.RemoteViewIndex = (ent.Comp.RemoteViewIndex + 1) % candidates.Count;
        _eye.SetTarget(ent.Owner, candidates[ent.Comp.RemoteViewIndex]);
        UpdateRemoteViewMovementLock(ent, candidates[ent.Comp.RemoteViewIndex]);
        args.Handled = true;
    }

    private void OnRemoteViewPreviousAction(Entity<TerrorSpiderPrincessComponent> ent, ref TerrorSpiderMotherRemoteViewPreviousActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EyeComponent>(ent.Owner, out _))
            return;

        var candidates = GetRemoteViewCandidates(ent.Owner);

        if (candidates.Count == 0)
        {
            _eye.SetTarget(ent.Owner, null);
            ent.Comp.RemoteViewIndex = -1;
            UpdateRemoteViewMovementLock(ent, null);
            args.Handled = true;
            return;
        }

        if (ent.Comp.RemoteViewIndex < 0 || ent.Comp.RemoteViewIndex >= candidates.Count)
            ent.Comp.RemoteViewIndex = 0;

        ent.Comp.RemoteViewIndex = (ent.Comp.RemoteViewIndex - 1 + candidates.Count) % candidates.Count;
        _eye.SetTarget(ent.Owner, candidates[ent.Comp.RemoteViewIndex]);
        UpdateRemoteViewMovementLock(ent, candidates[ent.Comp.RemoteViewIndex]);
        args.Handled = true;
    }

    private void OnRemoteViewExitAction(Entity<TerrorSpiderPrincessComponent> ent, ref TerrorSpiderMotherRemoteViewExitActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EyeComponent>(ent.Owner, out _))
            return;

        ent.Comp.RemoteViewIndex = -1;
        _eye.SetTarget(ent.Owner, null);
        UpdateRemoteViewMovementLock(ent, null);
        args.Handled = true;
    }

    private void OnHiveSenseAction(Entity<TerrorSpiderPrincessComponent> ent, ref TerrorSpiderPrincessHiveSenseActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActorComponent>(ent.Owner, out var actor))
            return;

        var lines = BuildHiveSenseLines(ent.Owner);
        if (lines.Count == 0)
            lines.Add(Loc.GetString("terror-spider-hive-sense-empty"));

        foreach (var line in lines)
        {
            _chat.DispatchServerMessage(actor.PlayerSession, line);
        }

        args.Handled = true;
    }

    private void OnScreamAction(Entity<TerrorSpiderPrincessComponent> ent, ref TerrorSpiderPrincessScreamActionEvent args)
    {
        if (args.Handled)
            return;

        var origin = Transform(ent.Owner).MapPosition;
        var rangeSquared = ent.Comp.ScreamRange * ent.Comp.ScreamRange;
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var mobState, out var xform))
        {
            if (uid == ent.Owner)
                continue;

            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (xform.MapPosition.MapId != origin.MapId)
                continue;

            if ((xform.MapPosition.Position - origin.Position).LengthSquared() > rangeSquared)
                continue;

            if (!IsTerrorSpider(uid))
            {
                _movementModStatus.TryUpdateMovementSpeedModDuration(
                    uid,
                    MovementModStatusSystem.TaserSlowdown,
                    TimeSpan.FromSeconds(ent.Comp.ScreamSlowDuration),
                    ent.Comp.ScreamSlowMultiplier);

                _stamina.TakeStaminaDamage(uid, ent.Comp.ScreamStaminaDamage, source: ent.Owner);
            }

            if (HasComp<SiliconLawBoundComponent>(uid))
            {
                _emp.EmpPulse(
                    xform.Coordinates,
                    0.2f,
                    ent.Comp.ScreamEmpEnergyConsumption,
                    TimeSpan.FromSeconds(ent.Comp.ScreamRobotDisableSeconds),
                    ent.Owner);
            }
        }

        if (ent.Comp.ScreamSound != null)
            _audio.PlayPvs(ent.Comp.ScreamSound, ent.Owner);

        args.Handled = true;
    }

    private void OnLayEggAction(Entity<TerrorSpiderPrincessComponent> ent, ref TerrorSpiderPrincessLayEggActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanLayEgg(ent, out _))
            return;

        var egg = Spawn(args.EggPrototype, Transform(ent.Owner).Coordinates);
        var eggComp = EnsureComp<TerrorSpiderPrincessEggComponent>(egg);
        eggComp.Princess = ent.Owner;
        eggComp.HatchAt = _timing.CurTime + TimeSpan.FromSeconds(eggComp.HatchDelay);
        args.Handled = true;
    }

    private bool CanLayEgg(Entity<TerrorSpiderPrincessComponent> ent, out int broodCount)
    {
        broodCount = 0;

        var eggs = EntityQueryEnumerator<TerrorSpiderPrincessEggComponent>();
        while (eggs.MoveNext(out _, out var eggComp))
        {
            if (eggComp.Princess == ent.Owner)
                return false;
        }

        var mapId = Transform(ent.Owner).MapID;
        var brood = EntityQueryEnumerator<TerrorSpiderPrincessBroodComponent, MobStateComponent, TransformComponent>();
        while (brood.MoveNext(out _, out var broodComp, out var state, out var xform))
        {
            if (broodComp.Princess != ent.Owner)
                continue;

            if (state.CurrentState != MobState.Alive)
                continue;

            if (xform.MapID != mapId)
                continue;

            broodCount++;
        }

        return broodCount < ent.Comp.MaxBroodOnMap;
    }

    private void UpdateRemoteViewMovementLock()
    {
        var query = EntityQueryEnumerator<TerrorSpiderPrincessComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryComp<EyeComponent>(uid, out var eye))
                continue;

            var speedMultiplier = eye.Target == null ? 1f : 0f;
            _movementModStatus.TryUpdateMovementSpeedModDuration(
                uid,
                comp.RemoteViewImmobileStatusEffect,
                TimeSpan.FromSeconds(comp.RemoteViewImmobileRefresh),
                speedMultiplier);
        }
    }

    private void UpdateRemoteViewMovementLock(Entity<TerrorSpiderPrincessComponent> ent, EntityUid? currentTarget)
    {
        var speedMultiplier = currentTarget == null ? 1f : 0f;
        _movementModStatus.TryUpdateMovementSpeedModDuration(
            ent.Owner,
            ent.Comp.RemoteViewImmobileStatusEffect,
            TimeSpan.FromSeconds(ent.Comp.RemoteViewImmobileRefresh),
            speedMultiplier);
    }

    private void UpdateEggs(TimeSpan now)
    {
        var eggs = EntityQueryEnumerator<TerrorSpiderPrincessEggComponent, TransformComponent>();
        while (eggs.MoveNext(out var uid, out var egg, out var xform))
        {
            if (egg.HatchAt == TimeSpan.Zero)
                egg.HatchAt = now + TimeSpan.FromSeconds(egg.HatchDelay);

            if (now < egg.HatchAt)
                continue;

            var hatchProto = SelectHatchPrototype(egg, xform.MapID);
            if (hatchProto == null)
            {
                QueueDel(uid);
                continue;
            }

            var spawned = Spawn(hatchProto, xform.Coordinates);
            if (egg.Princess != null && !Deleted(egg.Princess.Value))
            {
                var brood = EnsureComp<TerrorSpiderPrincessBroodComponent>(spawned);
                brood.Princess = egg.Princess;
                brood.Elite = egg.TierTwo && egg.TierTwoElitePrototypes.Contains(hatchProto);
            }

            QueueDel(uid);
        }
    }

    private void UpdateOrphanDamage(TimeSpan now)
    {
        var query = EntityQueryEnumerator<TerrorSpiderPrincessOrphanDamageComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var orphan, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (orphan.NextTick == TimeSpan.Zero)
                orphan.NextTick = now + TimeSpan.FromSeconds(orphan.TickInterval);

            if (now < orphan.NextTick)
                continue;

            orphan.NextTick = now + TimeSpan.FromSeconds(orphan.TickInterval);
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = FixedPoint2.New(orphan.DamagePerTick);
            _damageable.TryChangeDamage(uid, damage, ignoreResistances: false, interruptsDoAfters: false);
        }
    }

    private string? SelectHatchPrototype(TerrorSpiderPrincessEggComponent egg, MapId mapId)
    {
        if (!egg.TierTwo)
        {
            if (egg.TierOnePrototypes.Count == 0)
                return null;

            return _random.Pick(egg.TierOnePrototypes);
        }

        var allowed = egg.TierTwoPrototypes;

        if (egg.Princess != null
            && TryComp<TerrorSpiderPrincessComponent>(egg.Princess.Value, out var princessComp)
            && CountEliteBroodOnMap(egg.Princess.Value, mapId) >= princessComp.MaxEliteBroodOnMap)
        {
            allowed = egg.TierTwoUnlimitedPrototypes;
        }

        if (allowed.Count == 0)
            return null;

        return _random.Pick(allowed);
    }

    private int CountEliteBroodOnMap(EntityUid princess, MapId mapId)
    {
        var count = 0;
        var query = EntityQueryEnumerator<TerrorSpiderPrincessBroodComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out _, out var brood, out var mob, out var xform))
        {
            if (brood.Princess != princess || !brood.Elite)
                continue;

            if (mob.CurrentState != MobState.Alive)
                continue;

            if (xform.MapID != mapId)
                continue;

            count++;
        }

        return count;
    }

    

    private bool IsTerrorSpider(EntityUid uid)
    {
        return HasComp<TerrorSpiderWebBuffReceiverComponent>(uid) || _tagSystem.HasTag(uid, _terrorSpiderTag);
    }

    private List<string> BuildHiveSenseLines(EntityUid princess)
    {
        var lines = new List<string>();
        var query = EntityQueryEnumerator<TerrorSpiderPrincessBroodComponent, MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var brood, out var mobState, out var xform))
        {
            if (brood.Princess != princess)
                continue;

            if (mobState.CurrentState != MobState.Alive)
                continue;

            var hp = 0f;
            var hpMax = 0f;

            if (TryComp<DamageableComponent>(uid, out var damageable)
                && TryComp<MobThresholdsComponent>(uid, out var thresholds))
            {
                foreach (var (value, state) in thresholds.Thresholds)
                {
                    if (state == MobState.Dead)
                    {
                        hpMax = value.Float();
                        break;
                    }
                }

                hp = MathF.Max(0, hpMax - damageable.TotalDamage.Float());
            }

            var name = MetaData(uid).EntityName;
            var beacon = FindNearestBeaconName(xform.MapPosition);
            lines.Add(Loc.GetString("terror-spider-hive-sense-entry", ("name", name), ("hp", $"{hp:0}"), ("hpMax", $"{hpMax:0}"), ("beacon", beacon)));
        }

        return lines;
    }

    private string FindNearestBeaconName(MapCoordinates from)
    {
        EntityUid? nearest = null;
        var bestDistance = float.MaxValue;
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (!beacon.Enabled)
                continue;

            if (xform.MapPosition.MapId != from.MapId)
                continue;

            var distance = (xform.MapPosition.Position - from.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            nearest = uid;
        }

        if (nearest == null)
            return Loc.GetString("terror-spider-hive-sense-unknown");

        if (TryComp<NavMapBeaconComponent>(nearest, out var beaconComp) && !string.IsNullOrWhiteSpace(beaconComp.Text))
            return beaconComp.Text;

        return MetaData(nearest.Value).EntityName;
    }

    private List<EntityUid> GetRemoteViewCandidates(EntityUid princess)
    {
        var candidates = new List<EntityUid>();
        var princessPos = Transform(princess).MapPosition;
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var mob, out var xform))
        {
            if (uid == princess)
                continue;

            if (mob.CurrentState != MobState.Alive)
                continue;

            if (!IsTerrorSpider(uid))
                continue;

            if (xform.MapPosition.MapId != princessPos.MapId)
                continue;

            candidates.Add(uid);
        }

        candidates.Sort((a, b) => a.Id.CompareTo(b.Id));
        return candidates;
    }
}
