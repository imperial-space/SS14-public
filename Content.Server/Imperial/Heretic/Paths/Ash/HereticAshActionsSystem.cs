using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Beam;
using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Content.Server.Damage.Systems;
using Content.Server.DoAfter;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Electrocution;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Ash;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Content.Shared.Slippery;
using Content.Shared.Speech.Muting;
using Content.Shared.Stacks;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic.Paths.Ash;

/// <summary>
/// Способности пути Пепла.
/// </summary>
public sealed class HereticAshActionsSystem : EntitySystem
{
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly GodmodeSystem _godmode = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly HereticAshSpiritSystem _ashSpiritSystem = default!;

    private static readonly EntProtoId GreatFireCascadeActionId = "ActionHereticGreatFireCascade";
    private static readonly EntProtoId AshSpiritFlameOathActionId = "ActionHereticAshSpiritFlameOath";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticAshOrbComponent, MoveEvent>(OnAshOrbMove);
        SubscribeLocalEvent<HereticAshOrbComponent, GetVisMaskEvent>(OnAshOrbGetVisMask);
        SubscribeLocalEvent<HereticAshOrbComponent, EntityTerminatingEvent>(OnAshOrbTerminating);
        SubscribeLocalEvent<HereticComponent, BeforeDamageChangedEvent>(OnHereticAshShiftDamage);
        SubscribeLocalEvent<HereticComponent, HereticAshenPassageActionEvent>(OnAshenPassage);
        SubscribeLocalEvent<HereticComponent, HereticVolcanoBlastActionEvent>(OnVolcanoBlast);
        SubscribeLocalEvent<HereticComponent, HereticAshlordsRebirthActionEvent>(OnAshlordsRebirth);
        SubscribeLocalEvent<HereticAshBladeComponent, MeleeHitEvent>(OnAshBladeMeleeHit);
        SubscribeLocalEvent<HereticComponent, HereticAshlordRiteActionEvent>(OnAshlordRite);
        SubscribeLocalEvent<HereticComponent, HereticFireRingOathActionEvent>(OnFireRingOath);
        SubscribeLocalEvent<HereticComponent, HereticFireCascadeActionEvent>(OnFireCascade);
        SubscribeLocalEvent<HereticComponent, HereticAshSpiritFlameOathActionEvent>(OnHereticFlameOath);
        SubscribeLocalEvent<HereticComponent, HereticGreatFireCascadeActionEvent>(OnGreatFireCascade);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
        SubscribeLocalEvent<HereticRemovedEvent>(OnHereticRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var shiftQuery = EntityQueryEnumerator<HereticAshShiftComponent>();
        while (shiftQuery.MoveNext(out var uid, out var shift))
        {
            if (shift.ExitTime is { } exitTime)
            {
                if (now >= exitTime)
                    FinishAshShift((uid, shift));
            }
            else if (now >= shift.EndTime)
            {
                EndAshShift(uid);
            }
        }

        var ringQuery = EntityQueryEnumerator<HereticFireRingOathComponent, MobStateComponent>();
        while (ringQuery.MoveNext(out var uid, out var ring, out var mobState))
        {
            if (now < ring.NextTick)
                continue;

            if (ring.TicksLeft <= 0 || !_mobs.IsAlive(uid, mobState))
            {
                RemCompDeferred<HereticFireRingOathComponent>(uid);
                continue;
            }

            ring.TicksLeft--;
            ring.NextTick += ring.TickInterval;
            BurnFireRing((uid, ring));
        }
    }

    private void OnHereticRemoved(ref HereticRemovedEvent args)
    {
        EndAshShift(args.Heretic, silent: true);
        RemCompDeferred<HereticFireRingOathComponent>(args.Heretic);
    }

    // ─── Ash ─────────────────────────────────────────────────────────────────

    private void OnAshenPassage(EntityUid uid, HereticComponent comp, HereticAshenPassageActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (HasComp<HereticAshShiftComponent>(uid))
            return;

        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return;

        var empowered = false;
        if (TryComp<FlammableComponent>(uid, out var fireComp) && fireComp.FireStacks > 3f)
        {
            foreach (var slot in new[] { "outerClothing", "outer" })
            {
                if (!_inventory.TryGetSlotEntity(uid, slot, out var slotEnt))
                    continue;

                if (!TryComp<HereticPathRobeComponent>(slotEnt.Value, out var robeComp) || robeComp.Path != HereticPath.Ash)
                    continue;

                empowered = true;
                break;
            }
        }

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-ashen-passage"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        var coords = Transform(uid).Coordinates;
        var orbUid = Spawn("MobHereticAshOrb", coords);

        if (!TryComp<HereticAshOrbComponent>(orbUid, out var orbComp))
        {
            QueueDel(orbUid);
            return;
        }

        orbComp.HereticUid = uid;
        orbComp.Empowered = empowered;

        var vis = EnsureComp<VisibilityComponent>(orbUid);
        _visibility.AddLayer((orbUid, vis), (int)VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer((orbUid, vis), (int)VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(orbUid, visibilityComponent: vis);

        _xform.DetachEntity(uid, Transform(uid));

        _mind.TransferTo(mindId, orbUid, ghostCheckOverride: true);
        _eye.RefreshVisibilityMask(orbUid);

        _godmode.EnableGodmode(orbUid);

        var shift = EnsureComp<HereticAshShiftComponent>(uid);
        shift.Orb = orbUid;
        shift.EndTime = _timing.CurTime + shift.Duration;

        if (empowered)
        {
            RemCompDeferred<KnockedDownComponent>(uid);
            RemCompDeferred<StunnedComponent>(uid);

            if (TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0)
                _cuffs.TryUncuff(uid, uid);

            if (TryComp<FlammableComponent>(uid, out var fireComp2))
                _flammable.Extinguish(uid, fireComp2);
        }

        Spawn("HereticEffectAshBlink", coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_ethereal_enter.ogg"), orbUid);
        _popup.PopupEntity(Loc.GetString(empowered ? "heretic-ash-passage-empowered" : "heretic-ash-passage"), orbUid, orbUid, PopupType.Medium);
    }

    private void OnAshOrbMove(EntityUid uid, HereticAshOrbComponent comp, ref MoveEvent args)
    {
        if (!TryComp<HereticAshShiftComponent>(comp.HereticUid, out var shift) || shift.ExitTime != null)
            return;

        if (_turf.TryGetTileRef(args.NewPosition, out var tileRef)
            && !_turf.IsTileBlocked(tileRef.Value, CollisionGroup.Impassable))
        {
            shift.ExitPoints.Add(args.NewPosition);
            if (shift.ExitPoints.Count > shift.MaxExitPoints)
                shift.ExitPoints.RemoveAt(0);
        }
    }

    private void OnAshOrbGetVisMask(EntityUid uid, HereticAshOrbComponent comp, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int)VisibilityFlags.Ghost;
    }

    private void OnAshOrbTerminating(EntityUid uid, HereticAshOrbComponent comp, ref EntityTerminatingEvent args)
    {
        EndAshShift(comp.HereticUid, silent: true);
    }

    private void OnHereticAshShiftDamage(EntityUid uid, HereticComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (TryComp<HereticAshShiftComponent>(uid, out var shift) && shift.ExitTime == null)
            args.Cancelled = true;
    }

    private void OnVolcanoBlast(EntityUid uid, HereticComponent comp, HereticVolcanoBlastActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        var maxBounces = (comp.AscensionTriggered && comp.CurrentPath == HereticPath.Ash) ? 8 : 4;
        const float chainRadius = 7f;

        // SS13: empowered if ash robes worn + fire_stacks > 3 → consume all stacks
        var empowered = false;
        if (TryComp<FlammableComponent>(uid, out var casterFlam) && casterFlam.FireStacks > 3f)
        {
            empowered = true;
            _flammable.AdjustFireStacks(uid, -casterFlam.FireStacks);
        }

        var hitSet = new HashSet<EntityUid> { uid };
        var firstTarget = GetVolcanoNextTarget(uid, uid, hitSet, chainRadius);

        if (firstTarget == null)
        {
            _popup.PopupEntity(Loc.GetString("heretic-volcano-blast-no-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/fireball.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-volcano-blast"), uid, uid, PopupType.Medium);
        if (empowered)
            _popup.PopupEntity(Loc.GetString("heretic-volcano-blast-empowered"), uid, uid, PopupType.LargeCaution);

        SendVolcanoBeam(uid, uid, firstTarget.Value, maxBounces, empowered, hitSet);
    }

    // beamSource — откуда вылетает луч (первый раз кастер, дальше — предыдущая жертва)
    private void SendVolcanoBeam(EntityUid caster, EntityUid beamSource, EntityUid target, int bounces, bool empowered, HashSet<EntityUid> hitSet)
    {
        if (Deleted(caster) || Deleted(beamSource) || Deleted(target))
            return;

        const float chainRadius = 7f;
        const float hitDamage = 20f;

        hitSet.Add(target);

        _beam.TryCreateBeam(beamSource, target, "HereticFireBeam");
        Spawn("HereticEffectFireExplosion", Transform(target).Coordinates);

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Heat"] = FixedPoint2.New(hitDamage);
        _damage.TryChangeDamage(target, dmg, ignoreResistances: false);
        _flammable.AdjustFireStacks(target, empowered ? 6f : 3f, ignite: true);

        if (bounces <= 0)
        {
            DoVolcanoAoE(target, hitSet, empowered);
            return;
        }

        Timer.Spawn(TimeSpan.FromSeconds(1.0), () =>
            ContinueVolcanoBeam(caster, target, bounces - 1, empowered, hitSet, chainRadius));
    }

    private void ContinueVolcanoBeam(EntityUid caster, EntityUid lastTarget, int bounces, bool empowered, HashSet<EntityUid> hitSet, float chainRadius)
    {
        if (Deleted(caster) || Deleted(lastTarget))
            return;
        if (!TryComp<MobStateComponent>(lastTarget, out var lastMobState) || !_mobs.IsAlive(lastTarget, lastMobState))
            return;
        if (!TryComp<FlammableComponent>(lastTarget, out var lastFlam) || !lastFlam.OnFire)
            return;

        var next = GetVolcanoNextTarget(caster, lastTarget, hitSet, chainRadius);
        if (next == null)
        {
            // Некого поджечь дальше — AoE на последней цели
            DoVolcanoAoE(lastTarget, hitSet, empowered);
            return;
        }

        SendVolcanoBeam(caster, lastTarget, next.Value, bounces, empowered, hitSet);
    }

    private void DoVolcanoAoE(EntityUid center, HashSet<EntityUid> hitSet, bool empowered)
    {
        const float aoeDamage = 15f;
        var coords = Transform(center).Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 1.5f))
        {
            var aoeTarget = ent.Owner;
            if (hitSet.Contains(aoeTarget))
                continue;
            if (!_mobs.IsAlive(aoeTarget, ent.Comp))
                continue;
            if (HasComp<HereticComponent>(aoeTarget))
                continue;

            var aoeDmg = new DamageSpecifier();
            aoeDmg.DamageDict["Heat"] = FixedPoint2.New(aoeDamage);
            _damage.TryChangeDamage(aoeTarget, aoeDmg, ignoreResistances: false);
            _flammable.AdjustFireStacks(aoeTarget, empowered ? 4f : 2f, ignite: true);
            _stun.TryKnockdown(aoeTarget, TimeSpan.FromSeconds(0.8), true);
        }
    }

    // SS13 get_target: random pick, priority = already burning mobs
    private EntityUid? GetVolcanoNextTarget(EntityUid caster, EntityUid searchCenter, HashSet<EntityUid> alreadyHit, float radius)
    {
        var centerCoords = Transform(searchCenter).Coordinates;
        var validTargets = new List<EntityUid>();
        var priorityTargets = new List<EntityUid>();

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(centerCoords, radius))
        {
            var target = ent.Owner;
            if (target == caster)
                continue;
            if (alreadyHit.Contains(target))
                continue;
            if (HasComp<HereticComponent>(target))
                continue;
            if (!_mobs.IsAlive(target, ent.Comp))
                continue;

            if (TryComp<FlammableComponent>(target, out var flam) && flam.OnFire)
                priorityTargets.Add(target);
            else
                validTargets.Add(target);
        }

        if (priorityTargets.Count > 0)
            return _random.Pick(priorityTargets);
        if (validTargets.Count > 0)
            return _random.Pick(validTargets);

        return null;
    }

    private void OnAshlordsRebirth(EntityUid uid, HereticComponent comp, HereticAshlordsRebirthActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (TryComp<FlammableComponent>(uid, out var selfFlam))
            _flammable.Extinguish(uid, selfFlam);

        var coords = Transform(uid).Coordinates;
        Spawn("HereticEffectFireExplosion", coords);

        var victimsHit = 0;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 14f))
        {
            if (ent.Owner == uid)
                continue;
            if (HasComp<HereticComponent>(ent.Owner))
                continue;
            if (!TryComp<FlammableComponent>(ent.Owner, out var flam) || !flam.OnFire)
                continue;
            if (ent.Comp.CurrentState == MobState.Dead)
                continue;

            // Instant kill mobs already in critical state (SS13: CAN_SUCCUMB check before damage)
            if (ent.Comp.CurrentState == MobState.Critical)
                _mobs.ChangeMobState(ent.Owner, MobState.Dead, ent.Comp);

            var victimDmg = new DamageSpecifier();
            victimDmg.DamageDict["Heat"] = FixedPoint2.New(20);
            _damage.TryChangeDamage(ent.Owner, victimDmg, ignoreResistances: false);
            _flammable.Extinguish(ent.Owner, flam);
            Spawn("HereticEffectAshBlink", Transform(ent.Owner).Coordinates);

            victimsHit++;
        }

        if (victimsHit > 0)
        {
            var perVictimVal = FixedPoint2.New(-10);
            var healDmg = new DamageSpecifier();
            healDmg.DamageDict["Blunt"] = perVictimVal * victimsHit;
            healDmg.DamageDict["Heat"] = perVictimVal * victimsHit;
            healDmg.DamageDict["Poison"] = perVictimVal * victimsHit;
            healDmg.DamageDict["Asphyxiation"] = perVictimVal * victimsHit;
            _damage.TryChangeDamage(uid, healDmg, ignoreResistances: true);
            _stamina.TakeStaminaDamage(uid, -10f * victimsHit, visual: false);

            var reducedCd = Math.Max(9, 60 - victimsHit * 10);
            var now = _timing.CurTime;
            foreach (var action in _actions.GetActions(uid))
            {
                if (!TryComp<InstantActionComponent>(action.Owner, out var iac))
                    continue;
                if (iac.Event is not HereticAshlordsRebirthActionEvent)
                    continue;
                _actions.SetCooldown((action.Owner, (ActionComponent?)action.Comp), now, now + TimeSpan.FromSeconds(reducedCd));
                break;
            }
        }

        _popup.PopupEntity(Loc.GetString("heretic-ashlords-rebirth"), uid, uid, PopupType.Large);
    }

    private void OnAshBladeMeleeHit(Entity<HereticAshBladeComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;
        if (!TryComp<HereticComponent>(args.User, out var comp))
            return;
        if (comp.CurrentPath != HereticPath.Ash)
            return;

        var hasFieryBlade = _heretic.HasKnowledge(comp, "KnowledgeFieryBlade");
        var hasMarkOfAsh = comp.CurrentPath == HereticPath.Ash;
        if (!hasFieryBlade && !hasMarkOfAsh)
            return;

        foreach (var target in args.HitEntities)
        {
            if (hasFieryBlade)
                _flammable.AdjustFireStacks(target, 1f, ignite: true);

            if (!hasMarkOfAsh || !HasComp<AshMarkComponent>(target))
                continue;

            var bonusDmg = new DamageSpecifier();
            bonusDmg.DamageDict["Stamina"] = FixedPoint2.New(10);
            bonusDmg.DamageDict["Heat"] = FixedPoint2.New(10);
            _damage.TryChangeDamage(target, bonusDmg, ignoreResistances: false);
            _flammable.AdjustFireStacks(target, 4f, ignite: true);
            Spawn("HereticEffectAshBlink", Transform(target).Coordinates);

            RemCompDeferred<AshMarkComponent>(target);

            foreach (var nearby in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(target).Coordinates, 5f))
            {
                if (nearby.Owner == args.User || nearby.Owner == target)
                    continue;
                if (!_mobs.IsAlive(nearby.Owner, nearby.Comp))
                    continue;
                EnsureComp<AshMarkComponent>(nearby.Owner);
                break;
            }

            foreach (var action in _actions.GetActions(args.User))
            {
                if (!TryComp<InstantActionComponent>(action.Owner, out var ia))
                    continue;
                if (ia.Event is not HereticMansusGraspActionEvent)
                    continue;
                if (action.Comp.Cooldown is not { } cd)
                    break;

                var now = _timing.CurTime;
                var remaining = cd.End - now;
                if (remaining > TimeSpan.Zero)
                    _actions.SetCooldown((action.Owner, (ActionComponent?)action.Comp), now, now + remaining * 0.25);
                break;
            }

            _popup.PopupEntity(Loc.GetString("heretic-ash-mark-triggered"), args.User, args.User, PopupType.Medium);
        }
    }

    /// <summary>
    /// Начинает выход еретика из формы сферы. При <paramref name="silent"/> тело возвращается сразу, без эффектов.
    /// </summary>
    private void EndAshShift(EntityUid hereticUid, bool silent = false)
    {
        if (!TryComp<HereticAshShiftComponent>(hereticUid, out var shift) || shift.ExitTime != null)
            return;

        var orbUid = shift.Orb;

        if (!_mind.TryGetMind(orbUid, out var mindId, out _))
        {
            if (!TerminatingOrDeleted(orbUid))
                QueueDel(orbUid);
            RemCompDeferred<HereticAshShiftComponent>(hereticUid);
            return;
        }

        var exitCoords = shift.ExitPoints.Count > 0
            ? shift.ExitPoints[^1]
            : (Exists(orbUid) ? Transform(orbUid).Coordinates : Transform(hereticUid).Coordinates);

        if (silent)
        {
            _xform.SetCoordinates(hereticUid, exitCoords);
            _mind.TransferTo(mindId, hereticUid, ghostCheckOverride: true);
            if (!TerminatingOrDeleted(orbUid))
                QueueDel(orbUid);
            RemCompDeferred<HereticAshShiftComponent>(hereticUid);
            return;
        }

        Spawn("HereticEffectAshBlink", exitCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_ethereal_exit.ogg"), exitCoords);

        shift.ExitTime = _timing.CurTime + shift.ExitDelay;
        shift.ExitCoordinates = exitCoords;
        shift.ExitMind = mindId;
    }

    /// <summary>
    /// Возвращает тело еретика в мир после задержки выхода и переносит в него разум.
    /// </summary>
    private void FinishAshShift(Entity<HereticAshShiftComponent> ent)
    {
        var (uid, shift) = ent;
        _xform.SetCoordinates(uid, shift.ExitCoordinates);

        var mind = _mind.TryGetMind(shift.Orb, out var currentMind, out _) ? currentMind : shift.ExitMind;
        _mind.TransferTo(mind, uid, ghostCheckOverride: true);

        _stun.TryKnockdown(uid, shift.ExitKnockdown, true);

        if (!TerminatingOrDeleted(shift.Orb))
            QueueDel(shift.Orb);

        RemCompDeferred<HereticAshShiftComponent>(uid);
    }

    // ─── Ash (expanded) ──────────────────────────────────────────────────────

    private void OnAshlordRite(EntityUid uid, HereticComponent comp, HereticAshlordRiteActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        // SS13: требует 3 обугленных или горящих трупа поблизости
        var corpses = new List<EntityUid>();
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 5f))
        {
            if (ent.Owner == uid)
                continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var mobState))
                continue;
            if (mobState.CurrentState != MobState.Dead)
                continue;
            if (!TryComp<FlammableComponent>(ent.Owner, out var flamCorpse) || flamCorpse.FireStacks <= 0f)
                continue;
            corpses.Add(ent.Owner);
            if (corpses.Count >= 3)
                break;
        }

        if (corpses.Count < 3)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ashlord-rite-fail"), uid, uid, PopupType.SmallCaution);
            args.Handled = false;
            return;
        }

        // Сжигаем трупы
        foreach (var corpse in corpses)
            QueueDel(corpse);

        // Огненное торнадо: несколько взрывов вокруг
        var xformUid = Transform(uid);
        var pos = xformUid.Coordinates;
        Spawn("HereticEffectFireExplosion", pos);
        for (var i = 0; i < 6; i++)
        {
            var angle = i * (Math.PI * 2 / 6);
            var offset = new Vector2((float)Math.Cos(angle) * 2f, (float)Math.Sin(angle) * 2f);
            Spawn("HereticEffectFireExplosion", pos.Offset(offset));
        }

        // AoE: большой огонь + поджог
        var fireDmg = new DamageSpecifier();
        fireDmg.DamageDict["Heat"] = FixedPoint2.New(30);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(pos, 5f))
        {
            if (ent.Owner == uid)
                continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var ms))
                continue;
            if (ms.CurrentState == MobState.Dead)
                continue;
            _damage.TryChangeDamage(ent.Owner, fireDmg, ignoreResistances: false);
            _flammable.AdjustFireStacks(ent.Owner, 5f, ignite: true);
        }

        // Исцеление еретику за ритуал
        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = FixedPoint2.New(-40);
        heal.DamageDict["Slash"] = FixedPoint2.New(-40);
        heal.DamageDict["Heat"] = FixedPoint2.New(-20);
        _damage.TryChangeDamage(uid, heal, ignoreResistances: true);

        // SS13: fire shield — ring of fire around caster for 60s (same as FireRingOath)
        var shieldDmg = new DamageSpecifier();
        shieldDmg.DamageDict["Heat"] = FixedPoint2.New(5);
        StartFireRingOath(uid, shieldDmg, 120);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse_curse2.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-ashlord-rite"), uid, uid, PopupType.Large);
    }

    private void OnFireRingOath(EntityUid uid, HereticComponent comp, HereticFireRingOathActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        // SS13 fire_sworn: ring of fire around caster for 60s, 5 Heat / 0.5s to nearby mobs in radius 2
        var coords = Transform(uid).Coordinates;
        Spawn("HereticEffectFireExplosion", coords);
        _popup.PopupEntity(Loc.GetString("heretic-fire-ring-oath"), uid, uid, PopupType.Large);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/fireball.ogg"), uid);

        var ringDmg = new DamageSpecifier();
        ringDmg.DamageDict["Heat"] = FixedPoint2.New(5);
        StartFireRingOath(uid, ringDmg, 120); // 60s / 0.5s
    }

    private void StartFireRingOath(EntityUid casterUid, DamageSpecifier ringDmg, int ticks)
    {
        var ring = EnsureComp<HereticFireRingOathComponent>(casterUid);
        ring.Damage = ringDmg;
        ring.TicksLeft = ticks;
        ring.NextTick = _timing.CurTime + ring.TickInterval;
    }

    private void BurnFireRing(Entity<HereticFireRingOathComponent> ring)
    {
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ring).Coordinates, ring.Comp.Radius))
        {
            if (ent.Owner == ring.Owner || !_mobs.IsAlive(ent.Owner, ent.Comp))
                continue;

            _damage.TryChangeDamage(ent.Owner, ring.Comp.Damage, ignoreResistances: false);
            _flammable.AdjustFireStacks(ent.Owner, ring.Comp.FireStacks, ignite: true);
        }
    }

    private void OnFireCascade(EntityUid uid, HereticComponent comp, HereticFireCascadeActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        // SS13 fire_cascade: expanding ring of fire — 3 expanding pulses (inner → outer)
        var targetCoords = args.Target;
        Spawn("HereticEffectFireExplosion", targetCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/fireball.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-fire-cascade"), uid, uid, PopupType.Medium);

        var alreadyHit = new HashSet<EntityUid> { uid };
        var innerDmg = new DamageSpecifier();
        innerDmg.DamageDict["Heat"] = FixedPoint2.New(5);
        var outerDmg = new DamageSpecifier();
        outerDmg.DamageDict["Heat"] = FixedPoint2.New(8);

        // Wave 1 — immediate, radius 1.5
        FireCascadeWave(targetCoords, 1.5f, innerDmg, 1f, alreadyHit);

        // Wave 2 — t+250ms, radius 2.5
        Timer.Spawn(250, () =>
        {
            if (!Exists(uid))
                return;
            Spawn("HereticEffectFireExplosion", targetCoords);
            FireCascadeWave(targetCoords, 2.5f, innerDmg, 1.5f, alreadyHit);
        });

        // Wave 3 — t+500ms, radius 4 (outermost, hits harder)
        Timer.Spawn(500, () =>
        {
            if (!Exists(uid))
                return;
            Spawn("HereticEffectFireExplosion", targetCoords);
            FireCascadeWave(targetCoords, 4f, outerDmg, 2f, alreadyHit);
        });
    }

    private void FireCascadeWave(EntityCoordinates center, float radius, DamageSpecifier dmg, float fireStacks, HashSet<EntityUid> alreadyHit)
    {
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(center, radius))
        {
            if (alreadyHit.Contains(ent.Owner))
                continue;
            if (!_mobs.IsAlive(ent.Owner, ent.Comp))
                continue;
            alreadyHit.Add(ent.Owner);
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _flammable.AdjustFireStacks(ent.Owner, fireStacks, ignite: true);
            Spawn("HereticEffectFireExplosion", Transform(ent.Owner).Coordinates);
        }
    }

    // ─── Ascension Ash flame abilities ───────────────────────────────────────

    private void OnHereticFlameOath(EntityUid uid, HereticComponent comp, HereticAshSpiritFlameOathActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        _ashSpiritSystem.StartFlameOath(uid, 300);
    }

    private void OnGreatFireCascade(EntityUid uid, HereticComponent comp, HereticGreatFireCascadeActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/fireball.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-great-fire-cascade"), uid, uid, PopupType.Large);

        var alreadyHit = new HashSet<EntityUid> { uid };
        FireCascadeRing(uid, 1, alreadyHit);
    }

    private void FireCascadeRing(EntityUid uid, int radius, HashSet<EntityUid> alreadyHit)
    {
        if (radius > 6)
            return;

        var origin = Transform(uid).Coordinates;
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Heat"] = FixedPoint2.New(10);

        for (var dx = -radius; dx <= radius; dx++)
            for (var dy = -radius; dy <= radius; dy++)
            {
                if ((int)Math.Round(Math.Sqrt(dx * dx + dy * dy)) != radius)
                    continue;
                var tileCoords = origin.Offset(new Vector2(dx, dy));
                Spawn("HereticAshSpiritFire", tileCoords);

                foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(tileCoords, 0.7f))
                {
                    if (alreadyHit.Contains(ent.Owner))
                        continue;
                    if (!_mobs.IsAlive(ent.Owner, ent.Comp))
                        continue;
                    alreadyHit.Add(ent.Owner);
                    _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
                    _flammable.AdjustFireStacks(ent.Owner, 3f, ignite: true);
                    Spawn("HereticEffectFireExplosion", Transform(ent.Owner).Coordinates);
                }
            }

        Timer.Spawn(300, () =>
        {
            if (!Exists(uid))
                return;
            FireCascadeRing(uid, radius + 1, alreadyHit);
        });
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Ash)
            return;

        var uid = args.Heretic;
        var comp = args.Component;

        EntityUid? ringAction = null;
        _actions.AddAction(uid, ref ringAction, AshSpiritFlameOathActionId);
        if (ringAction.HasValue)
            _heretic.AddGrantedAction(uid, comp, ringAction.Value);

        EntityUid? cascadeAction = null;
        _actions.AddAction(uid, ref cascadeAction, GreatFireCascadeActionId);
        if (cascadeAction.HasValue)
            _heretic.AddGrantedAction(uid, comp, cascadeAction.Value);

        foreach (var action in _actions.GetActions(uid))
        {
            if (!TryComp<InstantActionComponent>(action.Owner, out var ia))
                continue;
            if (ia.Event is not HereticVolcanoBlastActionEvent)
                continue;
            if (TryComp<ActionComponent>(action.Owner, out var ac) && ac.UseDelay.HasValue)
                _actions.SetUseDelay(new Entity<ActionComponent?>(action.Owner, ac), TimeSpan.FromSeconds(ac.UseDelay.Value.TotalSeconds * 0.66));
            break;
        }
        foreach (var action in _actions.GetActions(uid))
        {
            if (!TryComp<InstantActionComponent>(action.Owner, out var ia))
                continue;
            if (ia.Event is not HereticAshlordsRebirthActionEvent)
                continue;
            if (TryComp<ActionComponent>(action.Owner, out var ac) && ac.UseDelay.HasValue)
                _actions.SetUseDelay(new Entity<ActionComponent?>(action.Owner, ac), TimeSpan.FromSeconds(ac.UseDelay.Value.TotalSeconds * 0.16));
            break;
        }

        // SS13 traits: TRAIT_NOFIRE, TRAIT_NOBREATH, TRAIT_RESISTCOLD, TRAIT_RESISTHEAT, TRAIT_RESIST*PRESSURE
        if (TryComp<FlammableComponent>(uid, out var flam))
            flam.Damage = new DamageSpecifier();
        RemComp<RespiratorComponent>(uid);
        if (TryComp<TemperatureDamageComponent>(uid, out var tempDmg))
        {
            tempDmg.ColdDamageThreshold = 0f;
            tempDmg.HeatDamageThreshold = 99999f;
        }
        EnsureComp<PressureImmunityComponent>(uid);

        _popup.PopupEntity(Loc.GetString("heretic-ascension-ash"), uid, uid, PopupType.Large);
    }
}
