using Content.Server.Actions;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Chat.Managers;
using Content.Server.Imperial.TerrorSpider.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Tag;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderQueenSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> _terrorSpiderTag = "TerrorSpider";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly MovementModStatusSystem _movementModStatus = default!;
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly WeldableSystem _weldable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderQueenComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderMotherRemoteViewNextActionEvent>(OnRemoteViewNextAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderMotherRemoteViewPreviousActionEvent>(OnRemoteViewPreviousAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderMotherRemoteViewExitActionEvent>(OnRemoteViewExitAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderQueenCreateHiveActionEvent>(OnCreateHiveAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderQueenScreamActionEvent>(OnScreamAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderQueenHiveCountActionEvent>(OnHiveCountAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderQueenLayEggActionEvent>(OnLayEggAction);
        SubscribeLocalEvent<TerrorSpiderQueenComponent, TerrorSpiderVentUnweldActionEvent>(OnUnweldVentAction);
        SubscribeLocalEvent<TerrorSpiderQueenEggComponent, MapInitEvent>(OnEggMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateRemoteViewMovementLock();
        UpdateHiveSpeedDebuff();
        UpdateEggs(now);
        UpdateOrphanDamage(now);
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderQueenComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.CreateHiveActionEntity, comp.CreateHiveAction);
        _actions.AddAction(uid, ref comp.ScreamActionEntity, comp.ScreamAction);
        _actions.AddAction(uid, ref comp.RemoteViewNextActionEntity, comp.RemoteViewNextAction);
        _actions.AddAction(uid, ref comp.RemoteViewPreviousActionEntity, comp.RemoteViewPreviousAction);
        _actions.AddAction(uid, ref comp.RemoteViewExitActionEntity, comp.RemoteViewExitAction);
        _actions.AddAction(uid, ref comp.UnweldVentActionEntity, comp.UnweldVentAction);

        if (comp.HiveCreated)
            AddHiveActions(uid, comp);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderQueenComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.CreateHiveActionEntity);
        _actions.RemoveAction(uid, comp.ScreamActionEntity);
        _actions.RemoveAction(uid, comp.RemoteViewNextActionEntity);
        _actions.RemoveAction(uid, comp.RemoteViewPreviousActionEntity);
        _actions.RemoveAction(uid, comp.RemoteViewExitActionEntity);
        _actions.RemoveAction(uid, comp.UnweldVentActionEntity);
        RemoveHiveActions(uid, comp);
    }

    private void OnMobStateChanged(Entity<TerrorSpiderQueenComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var broodQuery = EntityQueryEnumerator<TerrorSpiderQueenBroodComponent>();
        while (broodQuery.MoveNext(out var uid, out var brood))
        {
            if (brood.Queen != ent.Owner)
                continue;

            var orphan = EnsureComp<TerrorSpiderQueenOrphanDamageComponent>(uid);
            orphan.DamagePerTick = ent.Comp.OrphanDamagePerTick;
            orphan.TickInterval = ent.Comp.OrphanDamageInterval;
            orphan.NextTick = TimeSpan.Zero;
        }
    }

    private void OnUnweldVentAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderVentUnweldActionEvent args)
    {
        if (args.Handled)
            return;

        if (!IsWeldedVent(args.Target))
            return;

        _weldable.SetWeldedState(args.Target, false);
        args.Handled = true;
    }

    private bool IsWeldedVent(EntityUid uid)
    {
        if (!HasComp<GasVentPumpComponent>(uid)
            && !HasComp<GasVentScrubberComponent>(uid)
            && !HasComp<GasPassiveVentComponent>(uid))
            return false;

        return _weldable.IsWelded(uid);
    }

    private void OnEggMapInit(Entity<TerrorSpiderQueenEggComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.HatchAt != TimeSpan.Zero)
            return;

        ent.Comp.HatchAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.HatchDelay);
    }

    private void OnRemoteViewNextAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderMotherRemoteViewNextActionEvent args)
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

    private void OnRemoteViewPreviousAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderMotherRemoteViewPreviousActionEvent args)
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

    private void OnRemoteViewExitAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderMotherRemoteViewExitActionEvent args)
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

    private void OnCreateHiveAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderQueenCreateHiveActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.HiveCreated)
            return;

        ent.Comp.HiveCreated = true;
        AddHiveActions(ent.Owner, ent.Comp);
        _actions.RemoveAction(ent.Owner, ent.Comp.CreateHiveActionEntity);
        ent.Comp.CreateHiveActionEntity = null;

        if (TryComp<MeleeWeaponComponent>(ent.Owner, out var melee))
        {
            melee.Damage.DamageDict["Structural"] = FixedPoint2.New(ent.Comp.HiveStructuralDamage);
            Dirty(ent.Owner, melee);
        }

        ApplyScream(ent.Owner, ent.Comp);
        args.Handled = true;
    }

    private void OnScreamAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderQueenScreamActionEvent args)
    {
        if (args.Handled)
            return;

        ApplyScream(ent.Owner, ent.Comp);
        args.Handled = true;
    }

    private void OnHiveCountAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderQueenHiveCountActionEvent args)
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

    private List<string> BuildHiveSenseLines(EntityUid queen)
    {
        var lines = new List<string>();
        var query = EntityQueryEnumerator<TerrorSpiderQueenBroodComponent, MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var brood, out var mobState, out var xform))
        {
            if (brood.Queen != queen)
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

                hp = MathF.Max(0, hpMax - _damageable.GetTotalDamage((uid, damageable)).Float());
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

    

    private void OnLayEggAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderQueenLayEggActionEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.HiveCreated)
            return;

        var now = _timing.CurTime;

        if (!string.IsNullOrWhiteSpace(args.SharedCooldownKey)
            && ent.Comp.SharedEggCooldownEnds.TryGetValue(args.SharedCooldownKey, out var sharedCooldownEnd)
            && now < sharedCooldownEnd)
        {
            return;
        }

        var royalGroup = !string.IsNullOrWhiteSpace(args.RoyalCooldownKey);
        if (HasActiveEggInGroup(ent.Owner, royalGroup))
            return;

        if (!string.IsNullOrWhiteSpace(args.RoyalCooldownKey)
            && ent.Comp.RoyalCooldownEnds.TryGetValue(args.RoyalCooldownKey, out var cooldownEnd)
            && now < cooldownEnd)
        {
            return;
        }

        var egg = Spawn(args.EggPrototype, Transform(ent.Owner).Coordinates);
        var eggComp = EnsureComp<TerrorSpiderQueenEggComponent>(egg);
        eggComp.Queen = ent.Owner;
        eggComp.HatchAt = now + TimeSpan.FromSeconds(eggComp.HatchDelay);
        eggComp.RoyalCooldownKey = args.RoyalCooldownKey;

        if (!string.IsNullOrWhiteSpace(args.SharedCooldownKey) && args.SharedCooldownSeconds > 0)
            ent.Comp.SharedEggCooldownEnds[args.SharedCooldownKey] = now + TimeSpan.FromSeconds(args.SharedCooldownSeconds);

        if (!string.IsNullOrWhiteSpace(args.RoyalCooldownKey))
            ent.Comp.RoyalCooldownEnds[args.RoyalCooldownKey] = now + TimeSpan.FromSeconds(ent.Comp.RoyalEggCooldownSeconds);

        args.Handled = true;
    }

    private bool HasActiveEggInGroup(EntityUid queen, bool royalGroup)
    {
        var eggs = EntityQueryEnumerator<TerrorSpiderQueenEggComponent>();
        while (eggs.MoveNext(out _, out var egg))
        {
            if (egg.Queen != queen)
                continue;

            var eggIsRoyal = !string.IsNullOrWhiteSpace(egg.RoyalCooldownKey);
            if (eggIsRoyal == royalGroup)
                return true;
        }

        return false;
    }

    private void UpdateRemoteViewMovementLock()
    {
        var query = EntityQueryEnumerator<TerrorSpiderQueenComponent>();
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

    private void UpdateRemoteViewMovementLock(Entity<TerrorSpiderQueenComponent> ent, EntityUid? currentTarget)
    {
        var speedMultiplier = currentTarget == null ? 1f : 0f;
        _movementModStatus.TryUpdateMovementSpeedModDuration(
            ent.Owner,
            ent.Comp.RemoteViewImmobileStatusEffect,
            TimeSpan.FromSeconds(ent.Comp.RemoteViewImmobileRefresh),
            speedMultiplier);
    }

    private void UpdateHiveSpeedDebuff()
    {
        var query = EntityQueryEnumerator<TerrorSpiderQueenComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.HiveCreated)
                continue;

            _movementModStatus.TryUpdateMovementSpeedModDuration(
                uid,
                comp.HiveSlowStatusEffect,
                TimeSpan.FromSeconds(comp.HiveSlowRefresh),
                comp.HiveSpeedMultiplier);
        }
    }

    private void UpdateEggs(TimeSpan now)
    {
        var eggs = EntityQueryEnumerator<TerrorSpiderQueenEggComponent, TransformComponent>();
        while (eggs.MoveNext(out var uid, out var egg, out var xform))
        {
            if (egg.HatchAt == TimeSpan.Zero)
                egg.HatchAt = now + TimeSpan.FromSeconds(egg.HatchDelay);

            if (now < egg.HatchAt)
                continue;

            if (string.IsNullOrWhiteSpace(egg.SpawnPrototype))
            {
                QueueDel(uid);
                continue;
            }

            var spawned = Spawn(egg.SpawnPrototype, xform.Coordinates);
            if (egg.Queen != null && !Deleted(egg.Queen.Value))
            {
                var brood = EnsureComp<TerrorSpiderQueenBroodComponent>(spawned);
                brood.Queen = egg.Queen;
                brood.RoyalCooldownKey = egg.RoyalCooldownKey;
            }

            QueueDel(uid);
        }
    }

    private void UpdateOrphanDamage(TimeSpan now)
    {
        var query = EntityQueryEnumerator<TerrorSpiderQueenOrphanDamageComponent, MobStateComponent>();
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

    private void ApplyScream(EntityUid queen, TerrorSpiderQueenComponent comp)
    {
        var origin = Transform(queen).MapPosition;
        var rangeSquared = comp.ScreamRange * comp.ScreamRange;
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var mobState, out var xform))
        {
            if (uid == queen)
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
                    TimeSpan.FromSeconds(comp.ScreamSlowDuration),
                    comp.ScreamSlowMultiplier);

                _stamina.TakeStaminaDamage(uid, comp.ScreamStaminaDamage, source: queen);
            }

            if (HasComp<SiliconLawBoundComponent>(uid))
            {
                _emp.EmpPulse(
                    xform.Coordinates,
                    0.2f,
                    comp.ScreamEmpEnergyConsumption,
                    TimeSpan.FromSeconds(comp.ScreamRobotDisableSeconds),
                    queen);
            }
        }

        BreakLightsInRange(queen, comp.LightBreakHalfRange);

        if (comp.ScreamSound != null)
            _audio.PlayPvs(comp.ScreamSound, queen);
    }

    private void BreakLightsInRange(EntityUid queen, float halfRange)
    {
        var queenPos = Transform(queen).MapPosition;
        var lightQuery = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (lightQuery.MoveNext(out var uid, out var light, out var xform))
        {
            if (xform.MapPosition.MapId != queenPos.MapId)
                continue;

            var delta = xform.MapPosition.Position - queenPos.Position;
            if (MathF.Abs(delta.X) > halfRange || MathF.Abs(delta.Y) > halfRange)
                continue;

            _poweredLight.TryDestroyBulb(uid, light);
        }
    }

    private List<EntityUid> GetRemoteViewCandidates(EntityUid queen)
    {
        var candidates = new List<EntityUid>();
        var queenPos = Transform(queen).MapPosition;
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var mob, out var xform))
        {
            if (uid == queen)
                continue;

            if (mob.CurrentState != MobState.Alive)
                continue;

            if (!IsTerrorSpider(uid))
                continue;

            if (xform.MapPosition.MapId != queenPos.MapId)
                continue;

            candidates.Add(uid);
        }

        candidates.Sort((a, b) => a.Id.CompareTo(b.Id));
        return candidates;
    }

    private bool IsTerrorSpider(EntityUid uid)
    {
        return HasComp<TerrorSpiderWebBuffReceiverComponent>(uid) || _tagSystem.HasTag(uid, _terrorSpiderTag);
    }

    private void AddHiveActions(EntityUid uid, TerrorSpiderQueenComponent comp)
    {
        _actions.AddAction(uid, ref comp.HiveCountActionEntity, comp.HiveCountAction);
        _actions.AddAction(uid, ref comp.LayEggRusarActionEntity, comp.LayEggRusarAction);
        _actions.AddAction(uid, ref comp.LayEggDronActionEntity, comp.LayEggDronAction);
        _actions.AddAction(uid, ref comp.LayEggLurkerActionEntity, comp.LayEggLurkerAction);
        _actions.AddAction(uid, ref comp.LayEggHealerActionEntity, comp.LayEggHealerAction);
        _actions.AddAction(uid, ref comp.LayEggReaperActionEntity, comp.LayEggReaperAction);
        _actions.AddAction(uid, ref comp.LayEggWidowActionEntity, comp.LayEggWidowAction);
        _actions.AddAction(uid, ref comp.LayEggGuardianActionEntity, comp.LayEggGuardianAction);
        _actions.AddAction(uid, ref comp.LayEggDestroyerActionEntity, comp.LayEggDestroyerAction);
        _actions.AddAction(uid, ref comp.LayEggPrinceActionEntity, comp.LayEggPrinceAction);
        _actions.AddAction(uid, ref comp.LayEggPrincessActionEntity, comp.LayEggPrincessAction);
        _actions.AddAction(uid, ref comp.LayEggMotherActionEntity, comp.LayEggMotherAction);
    }

    private void RemoveHiveActions(EntityUid uid, TerrorSpiderQueenComponent comp)
    {
        _actions.RemoveAction(uid, comp.HiveCountActionEntity);
        _actions.RemoveAction(uid, comp.LayEggRusarActionEntity);
        _actions.RemoveAction(uid, comp.LayEggDronActionEntity);
        _actions.RemoveAction(uid, comp.LayEggLurkerActionEntity);
        _actions.RemoveAction(uid, comp.LayEggHealerActionEntity);
        _actions.RemoveAction(uid, comp.LayEggReaperActionEntity);
        _actions.RemoveAction(uid, comp.LayEggWidowActionEntity);
        _actions.RemoveAction(uid, comp.LayEggGuardianActionEntity);
        _actions.RemoveAction(uid, comp.LayEggDestroyerActionEntity);
        _actions.RemoveAction(uid, comp.LayEggPrinceActionEntity);
        _actions.RemoveAction(uid, comp.LayEggPrincessActionEntity);
        _actions.RemoveAction(uid, comp.LayEggMotherActionEntity);
    }
}
