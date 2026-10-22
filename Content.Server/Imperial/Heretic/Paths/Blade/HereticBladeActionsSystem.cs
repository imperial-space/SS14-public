using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Beam;
using Content.Server.Body;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Damage.Systems;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Weapons.Ranged.Systems;
using Content.Server.Imperial.Antimagic;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Electrocution;
using Content.Shared.Eye;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Blade;
using Content.Shared.Imperial.Heretic.Paths.Lock;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
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
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.Server.Imperial.Heretic.Paths.Blade;

/// <summary>
/// Способности пути Клинка.
/// </summary>
public sealed class HereticBladeActionsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly NewStatusEffectsSystem _status = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly AlertsSystem _alert = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly HereticArenaSystem _arena = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ImperialAntimagicSystem _antimagic = default!;

    private static readonly ProtoId<TagPrototype> HereticBladeTag = "HereticBlade";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, DamageChangedEvent>(OnHereticDamaged);
        SubscribeLocalEvent<HereticComponent, DamageModifyEvent>(OnHereticDamageModify);
        SubscribeLocalEvent<HereticComponent, HereticRealignmentActionEvent>(OnRealignment);
        SubscribeLocalEvent<HereticComponent, HereticSanguineSurgeActionEvent>(OnSanguineSurge);
        SubscribeLocalEvent<HereticComponent, HereticCleaveActionEvent>(OnCleave);
        SubscribeLocalEvent<HereticComponent, HereticSummonBladesActionEvent>(OnSummonBlades);
        SubscribeLocalEvent<HereticComponent, HereticFuriousSteelActionEvent>(OnFuriousSteel);
        SubscribeLocalEvent<HereticComponent, HereticWolvesAmongSheepActionEvent>(OnWolvesAmongSheep);
        SubscribeLocalEvent<HereticBladeWeaponComponent, MeleeHitEvent>(OnBladeBladeMeleeHit);
        SubscribeLocalEvent<HereticBladeWeaponComponent, UseInHandEvent>(OnBladeUseInHand);
        SubscribeLocalEvent<HereticComponent, HereticRawRitualActionEvent>(OnRawRitual);
        SubscribeLocalEvent<HereticComponent, HereticStanceOfTornChampionActionEvent>(OnStanceOfTornChampion);
        SubscribeLocalEvent<TornChampionStanceComponent, BleedModifierEvent>(OnTornChampionBleed);
        SubscribeLocalEvent<TornChampionStanceComponent, DamageModifyEvent>(OnTornChampionDamageModify);
        SubscribeLocalEvent<TornChampionStanceComponent, KnockDownAttemptEvent>(OnTornChampionKnockdownAttempt);
        SubscribeLocalEvent<HereticComponent, HereticLionhunterRifleActionEvent>(OnLionhunterRifle);
        SubscribeLocalEvent<HereticMaelstromOfSilverComponent, StunnedEvent>(OnMaelstromStunned);
        SubscribeLocalEvent<HereticMaelstromOfSilverComponent, KnockDownAttemptEvent>(OnMaelstromKnockdownAttempt);
        SubscribeLocalEvent<HereticPathAscendedEvent>(OnPathAscended);
        SubscribeLocalEvent<HereticRemovedEvent>(OnHereticRemoved);
    }

    private void OnHereticRemoved(ref HereticRemovedEvent args)
    {
        RemCompDeferred<HereticDanceOfBrandComponent>(args.Heretic);
    }

    private void OnBladeBladeMeleeHit(Entity<HereticBladeWeaponComponent> blade, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;
        if (!TryComp<HereticComponent>(args.User, out var comp))
            return;

        // Lock path: Opening Blade — bleed on hit (65% after ascension, 35% before)
        if (comp.CurrentPath == HereticPath.Lock && _heretic.HasKnowledge(comp, "KnowledgeOpeningBlade"))
        {
            var bleedChance = HasComp<HereticLockAscendedComponent>(args.User) ? 0.65f : 0.35f;
            foreach (var target in args.HitEntities)
            {
                if (HasComp<HereticComponent>(target))
                    continue;
                if (!_random.Prob(bleedChance))
                    continue;
                _bloodstream.TryModifyBleedAmount(target, 10f);
            }
        }

        // Lock path: consume LockMark on blade hit — strip all access from the target's ID card
        if (comp.CurrentPath == HereticPath.Lock)
        {
            foreach (var target in args.HitEntities)
            {
                if (HasComp<HereticComponent>(target))
                    continue;
                _heretic.TryTriggerLockMark(args.User, target);
            }
        }

        if (comp.CurrentPath != HereticPath.Blade)
            return;

        if (HasComp<HereticMaelstromOfSilverComponent>(args.User))
        {
            foreach (var target in args.HitEntities)
            {
                if (target == args.User)
                    continue;
                if (_mobs.IsDead(target))
                    continue;

                // Blood steal
                if (TryComp<BloodstreamComponent>(target, out var targetBlood))
                    _bloodstream.TryModifyBloodLevel((target, targetBlood), -FixedPoint2.New(10));

                // SS13: on_eldritch_blade — bonus_damage ~10 BRUTE + self-heal brute/burn 5+5
                _damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Slash"] = FixedPoint2.New(10) } }, ignoreResistances: false);
                _damage.TryChangeDamage(args.User, new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(-5), ["Heat"] = FixedPoint2.New(-5) } }, ignoreResistances: true);
            }
        }

        // SS13: on_eldritch_blade — any blade hit consumes existing blade mark, gives orbiting blade
        if (comp.CurrentPath == HereticPath.Blade)
        {
            foreach (var target in args.HitEntities)
            {
                if (!_mobs.IsAlive(target) || target == args.User)
                    continue;
                _heretic.TryTriggerBladeMark(args.User, target);
            }
        }

        if (!_heretic.HasKnowledge(comp, "KnowledgeEmpoweredBlades"))
            return;

        foreach (var target in args.HitEntities)
            _heretic.TryEmpoweredBladesOnHit(args.User, comp, target, blade.Owner);
    }

    private void OnBladeUseInHand(Entity<HereticBladeWeaponComponent> blade, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<HereticComponent>(args.User, out var hComp))
            return;

        // After gaining aura (UnlimitedBlades), breaking is disabled
        if (hComp.UnlimitedBlades)
            return;

        args.Handled = true;

        var userXform = Transform(args.User);
        var destCoords = FindRandomSafeTileOnGrid(userXform);
        if (destCoords.HasValue)
            _xform.SetCoordinates(args.User, destCoords.Value);

        var afterMsg = hComp.CurrentPath switch
        {
            HereticPath.Blade => Loc.GetString("heretic-blade-shatter-blade"),
            HereticPath.Rust => Loc.GetString("heretic-blade-shatter-rust"),
            HereticPath.Ash => Loc.GetString("heretic-blade-shatter-ash"),
            HereticPath.Flesh => Loc.GetString("heretic-blade-shatter-flesh"),
            HereticPath.Void => Loc.GetString("heretic-blade-shatter-void"),
            HereticPath.Cosmos => Loc.GetString("heretic-blade-shatter-cosmos"),
            HereticPath.Lock => Loc.GetString("heretic-blade-shatter-lock"),
            HereticPath.Moon => Loc.GetString("heretic-blade-shatter-moon"),
            _ => Loc.GetString("heretic-blade-shatter"),
        };

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/runebreak.ogg"), args.User);
        _popup.PopupEntity(afterMsg, args.User, args.User, PopupType.MediumCaution);
        QueueDel(blade.Owner);
    }

    private EntityCoordinates? FindRandomSafeTileOnGrid(TransformComponent userXform)
    {
        var gridUid = userXform.GridUid;
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var gridEntity = new Entity<MapGridComponent>(gridUid.Value, grid);
        var tilesEnum = _map.GetAllTilesEnumerator(gridUid.Value, grid, ignoreEmpty: true);
        var candidates = new ValueList<Vector2i>();

        while (tilesEnum.MoveNext(out var tile))
        {
            if (_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable | CollisionGroup.MobMask))
                continue;
            candidates.Add(tile.Value.GridIndices);
        }

        if (candidates.Count == 0)
            return null;

        var chosen = candidates[_random.Next(candidates.Count)];
        return new EntityCoordinates(gridUid.Value, _map.TileCenterToVector(gridEntity, chosen));
    }

    // ─── Dance of the Brand (passive counter) ─────────────────────────────────

    private void OnHereticDamaged(EntityUid uid, HereticComponent comp, DamageChangedEvent args)
    {
        if (comp.CurrentPath != HereticPath.Blade)
            return;
        if (!_heretic.HasKnowledge(comp, "KnowledgeDanceOfBrand"))
            return;
        if (args.DamageDelta == null || args.DamageDelta.GetTotal() <= FixedPoint2.Zero)
            return;
        if (args.Origin is not EntityUid attacker || attacker == uid || Deleted(attacker))
            return;

        var now = _timing.CurTime;
        var dance = EnsureComp<HereticDanceOfBrandComponent>(uid);
        if (now < dance.NextTrigger)
            return;

        var holdsHereticBlade = false;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (!_tag.HasTag(held, HereticBladeTag))
                continue;

            holdsHereticBlade = true;
            break;
        }

        if (!holdsHereticBlade)
            return;

        dance.NextTrigger = now + dance.Cooldown;

        var counter = new DamageSpecifier();
        counter.DamageDict["Slash"] = FixedPoint2.New(15);
        _damage.TryChangeDamage(attacker, counter, ignoreResistances: false, origin: uid);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-dance-of-brand"), uid, uid, PopupType.Medium);
    }

    // ─── Blade ────────────────────────────────────────────────────────────────

    private void OnRealignment(EntityUid uid, HereticComponent comp, HereticRealignmentActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }

        args.Handled = true;

        var cooldown = TimeSpan.FromSeconds(6 + 6 * comp.RealignmentLevel);

        RemCompDeferred<KnockedDownComponent>(uid);
        RemCompDeferred<StunnedComponent>(uid);
        EnsureComp<PacifiedComponent>(uid);
        if (TryComp<StaminaComponent>(uid, out var staminaComp))
            _stamina.TakeStaminaDamage(uid, -staminaComp.StaminaDamage, visual: false);

        _alert.ShowAlert(uid, "HereticRealignment");

        var captured = uid;
        Timer.Spawn(TimeSpan.FromSeconds(8), () =>
        {
            if (!Deleted(captured))
            {
                RemCompDeferred<PacifiedComponent>(captured);
                _alert.ClearAlert(captured, "HereticRealignment");
            }
        });

        if (comp.RealignmentLevel < 10)
            comp.RealignmentLevel++;
        Dirty(uid, comp);

        _actions.SetCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp), cooldown);

        Timer.Spawn(TimeSpan.FromSeconds(90), () =>
        {
            if (Deleted(captured))
                return;
            if (!TryComp<HereticComponent>(captured, out var h))
                return;
            if (h.RealignmentLevel > 0)
            {
                h.RealignmentLevel--;
                Dirty(captured, h);
            }
        });

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-realignment"), uid, uid, PopupType.Large);
    }

    private void OnSanguineSurge(EntityUid uid, HereticComponent comp, HereticSanguineSurgeActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Slash"] = FixedPoint2.New(25);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 2f))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
        }
        Spawn("HereticEffectCleave", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-sanguine-surge"), uid, uid, PopupType.Medium);
    }

    private void OnCleave(EntityUid uid, HereticComponent comp, HereticCleaveActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        // Remove all bleeding from owner (SS13: remove all wounds on cast)
        _bloodstream.TryModifyBleedAmount(uid, -9999f);

        var healDmg = new DamageSpecifier();
        healDmg.DamageDict["Slash"] = FixedPoint2.New(-15);

        var targetCoords = args.Target;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(targetCoords, 1.5f))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            if (HasComp<HereticComponent>(ent.Owner))
                continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Slash"] = FixedPoint2.New(15);
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
            _damage.TryChangeDamage(uid, healDmg, ignoreResistances: true);
            Spawn("HereticEffectCleave", Transform(ent.Owner).Coordinates);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-cleave"), uid, uid, PopupType.Medium);
    }

    private void OnSummonBlades(EntityUid uid, HereticComponent comp, HereticSummonBladesActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (comp.OrbitingBlades.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-blades-already-active"), uid, uid, PopupType.Small);
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        var bladeLimit = comp.CurrentPath switch
        {
            HereticPath.Blade => 4,
            HereticPath.Flesh => 3,
            HereticPath.Lock => 2,
            _ => 1,
        };
        if (!comp.UnlimitedBlades && comp.BladesCreated >= bladeLimit)
        {
            _popup.PopupEntity(Loc.GetString("heretic-blades-limit-reached"), uid, uid, PopupType.SmallCaution);
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            var delay = i * 660;
            Timer.Spawn(delay, () =>
            {
                if (!TryComp<HereticComponent>(uid, out var hComp))
                    return;
                _heretic.SpawnOrbitingBlade(uid, hComp);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
            });
        }

        _popup.PopupEntity(Loc.GetString("heretic-furious-steel"), uid, uid, PopupType.Medium);
    }

    private void OnFuriousSteel(EntityUid uid, HereticComponent comp, HereticFuriousSteelActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (comp.OrbitingBlades.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-no-blades"), uid, uid, PopupType.Small);
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        var shooterPos = _xform.GetWorldPosition(Transform(uid));
        var targetPos = _xform.ToMapCoordinates(args.Target).Position;
        var direction = targetPos - shooterPos;

        if (direction == Vector2.Zero)
        {
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        _heretic.TryConsumeOrbitingBlade(uid, comp);

        var blade = Spawn("HereticThrownBlade", Transform(uid).Coordinates);
        _gun.ShootProjectile(blade, direction, Vector2.Zero, uid, uid, 9f);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-furious-steel-thrown"), uid, uid, PopupType.Large);

        if (comp.OrbitingBlades.Count > 0)
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));

        // SS13: protective_blades/recharging — 60s recharge after ascension
        if (HasComp<HereticMaelstromOfSilverComponent>(uid))
        {
            Timer.Spawn(TimeSpan.FromSeconds(60), () =>
            {
                if (Deleted(uid) || !HasComp<HereticMaelstromOfSilverComponent>(uid))
                    return;
                _heretic.SpawnOrbitingBlade(uid);
                _popup.PopupEntity(Loc.GetString("heretic-maelstrom-blade-recharged"), uid, uid, PopupType.Small);
            });
        }
    }

    private void OnHereticDamageModify(EntityUid uid, HereticComponent comp, DamageModifyEvent args)
    {
        if (comp.OrbitingBlades.Count == 0)
            return;
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        if (!_heretic.TryConsumeOrbitingBlade(uid, comp))
            return;

        args.Damage = new DamageSpecifier();
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/parry.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-blade-deflected"), uid, uid, PopupType.Medium);

        // SS13: protective_blades/recharging — 60s recharge after ascension
        if (HasComp<HereticMaelstromOfSilverComponent>(uid))
        {
            Timer.Spawn(TimeSpan.FromSeconds(60), () =>
            {
                if (Deleted(uid) || !HasComp<HereticMaelstromOfSilverComponent>(uid))
                    return;
                _heretic.SpawnOrbitingBlade(uid);
                _popup.PopupEntity(Loc.GetString("heretic-maelstrom-blade-recharged"), uid, uid, PopupType.Small);
            });
        }
    }

    private void OnWolvesAmongSheep(EntityUid uid, HereticComponent comp, HereticWolvesAmongSheepActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (_lookup.GetEntitiesInRange<HereticArenaComponent>(Transform(uid).Coordinates, 25f).Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-wolves-arena-exists"), uid, uid, PopupType.SmallCaution);
            _actions.ClearCooldown(new Entity<ActionComponent?>(args.Action.Owner, args.Action.Comp));
            return;
        }

        _chat.TrySendInGameICMessage(uid, "D'M'N XP'NS'N!", InGameICChatType.Speak, false, ignoreActionBlocker: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/airlock_open.ogg"), uid);

        var targetCoords = args.Target.SnapToGrid(EntityManager);

        // Периметр 19×19 тайлов (радиус 9)
        var walls = new System.Collections.Generic.List<EntityUid>();
        for (var dx = -9; dx <= 9; dx++)
        {
            for (var dy = -9; dy <= 9; dy++)
            {
                if (System.Math.Abs(dx) != 9 && System.Math.Abs(dy) != 9)
                    continue;
                var wall = Spawn("HereticArenaWall", targetCoords.Offset(new Vector2(dx, dy)));
                walls.Add(wall);
            }
        }

        var marker = Spawn("HereticArenaMarker", targetCoords);
        if (!TryComp<HereticArenaComponent>(marker, out var arena))
            return;

        arena.Heretic = uid;
        arena.Walls.AddRange(walls);

        // Пол арены — визуальные декали rose_stone
        var gridId = _xform.GetGrid(targetCoords);
        if (gridId != null)
        {
            arena.FloorGrid = gridId.Value;
            for (var dx = -8; dx <= 8; dx++)
            {
                for (var dy = -8; dy <= 8; dy++)
                {
                    var stateNum = _random.Next(1, 9);
                    var decalId = $"HereticRoseStone{stateNum}";
                    if (_decals.TryAddDecal(decalId, targetCoords.Offset(new Vector2(dx, dy)), out var addedId))
                        arena.FloorDecals.Add(addedId);
                }
            }
        }

        // Добавить всех не-еретиков в радиусе 9 как участников и выдать тренировочные клинки
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(targetCoords, 9f))
        {
            if (ent.Owner == uid)
                continue;
            if (HasComp<HereticComponent>(ent.Owner))
                continue;

            _arena.AddParticipant(ent.Owner, marker, arena);

            var blade = Spawn("HereticBladeBase", Transform(ent.Owner).Coordinates);
            if (TryComp<HereticArenaParticipantComponent>(ent.Owner, out var partComp))
                partComp.TrainingBlade = blade;
            _hands.TryPickupAnyHand(ent.Owner, blade);
        }

        _popup.PopupEntity(Loc.GetString("heretic-wolves-start"), uid, uid, PopupType.Large);
    }

    // ─── Blade (expanded) ────────────────────────────────────────────────────

    private void OnRawRitual(EntityUid uid, HereticComponent comp, HereticRawRitualActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Slash"] = FixedPoint2.New(15);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 3f))
        {
            if (ent.Owner == uid)
                continue;
            if (_antimagic.CanBlockMagic(ent.Owner))
                continue;
            _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
        }
        var selfDmg = new DamageSpecifier();
        selfDmg.DamageDict["Slash"] = FixedPoint2.New(10);
        _damage.TryChangeDamage(uid, selfDmg, ignoreResistances: true);
        Spawn("HereticEffectCleave", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-raw-ritual"), uid, uid, PopupType.Medium);
    }

    private void OnStanceOfTornChampion(EntityUid uid, HereticComponent comp, HereticStanceOfTornChampionActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;

        if (HasComp<TornChampionStanceComponent>(uid))
        {
            RemComp<TornChampionStanceComponent>(uid);
            _popup.PopupEntity(Loc.GetString("heretic-stance-torn-champion-off"), uid, uid, PopupType.Medium);
        }
        else
        {
            EnsureComp<TornChampionStanceComponent>(uid);
            _popup.PopupEntity(Loc.GetString("heretic-stance-torn-champion-on"), uid, uid, PopupType.Large);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), uid);
    }

    private void OnTornChampionBleed(Entity<TornChampionStanceComponent> ent, ref BleedModifierEvent args)
    {
        args.BleedAmount *= 0.5f;
        args.BleedReductionAmount *= 1.5f;
    }

    private void OnTornChampionDamageModify(EntityUid uid, TornChampionStanceComponent comp, DamageModifyEvent args)
    {
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        if (!_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold))
            return;
        if (_damage.GetTotalDamage(uid) < critThreshold * 0.5f)
            return;

        args.Damage *= 0.7f;
    }

    private void OnTornChampionKnockdownAttempt(Entity<TornChampionStanceComponent> ent, ref KnockDownAttemptEvent args)
    {
        if (!_mobThreshold.TryGetThresholdForState(ent.Owner, MobState.Critical, out var critThreshold))
            return;
        if (_damage.GetTotalDamage(ent.Owner) < critThreshold * 0.5f)
            return;

        args.Cancelled = true;
    }

    private void OnLionhunterRifle(EntityUid uid, HereticComponent comp, HereticLionhunterRifleActionEvent args)
    {
        if (args.Handled)
            return;
        if (!_heretic.HasFocus(uid, comp))
        {
            _popup.PopupEntity(Loc.GetString("heretic-focus-required"), uid, uid, PopupType.Medium);
            return;
        }
        args.Handled = true;
        if (args.Entity is not { } target)
            return;
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Slash"] = FixedPoint2.New(40);
        _damage.TryChangeDamage(target, dmg, ignoreResistances: false);
        Spawn("HereticEffectCleave", Transform(target).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-lionhunter-rifle"), uid, uid, PopupType.Medium);

        // SS220: "Прицеливание даёт теплозрение" — выстрел даёт временное теплозрение
        EnsureComp<ThermalEntityVisionComponent>(uid);
        Timer.Spawn(TimeSpan.FromSeconds(6), () =>
        {
            if (!TerminatingOrDeleted(uid))
                RemComp<ThermalEntityVisionComponent>(uid);
        });
    }

    private void OnMaelstromStunned(EntityUid uid, HereticMaelstromOfSilverComponent comp, ref StunnedEvent args)
    {
        // SS13: add_stun_absorption — max 45s absorbed, then 2-min recharge
        if (comp.StunAbsorptionRechargeDoneAt.HasValue && _timing.CurTime < comp.StunAbsorptionRechargeDoneAt.Value)
            return; // absorption depleted, stun passes through

        if (_status.TryGetTime(uid, SharedStunSystem.StunId, out var stunTime) && stunTime.EndEffectTime is { } stunEnd)
        {
            var remaining = stunEnd - _timing.CurTime;
            if (remaining > TimeSpan.Zero)
            {
                comp.StunAbsorbedSeconds += (float)remaining.TotalSeconds;
                if (comp.StunAbsorbedSeconds >= 45f)
                {
                    comp.StunAbsorbedSeconds = 0f;
                    comp.StunAbsorptionRechargeDoneAt = _timing.CurTime + TimeSpan.FromMinutes(2);
                    Timer.Spawn(TimeSpan.FromMinutes(2), () =>
                    {
                        if (!Deleted(uid) && TryComp<HereticMaelstromOfSilverComponent>(uid, out var c))
                            c.StunAbsorptionRechargeDoneAt = null;
                    });
                    _popup.PopupEntity(Loc.GetString("heretic-maelstrom-absorption-depleted"), uid, uid, PopupType.MediumCaution);
                }
            }
        }

        _stun.TryUnstun(uid);
        _statusEffects.TryRemoveStatusEffect(uid, "SlowedDown");
    }

    private void OnMaelstromKnockdownAttempt(EntityUid uid, HereticMaelstromOfSilverComponent comp, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnPathAscended(ref HereticPathAscendedEvent args)
    {
        if (args.Component.CurrentPath != HereticPath.Blade)
            return;

        var uid = args.Heretic;
        var comp = args.Component;
        var coords = Transform(uid).Coordinates;

        EnsureComp<HereticMaelstromOfSilverComponent>(uid);
        _heretic.ClearOrbitingBlades(uid, comp);
        _heretic.SpawnOrbitingBlade(uid, comp);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/unsheath.ogg"), uid, AudioParams.Default.WithVolume(-9.6f));
        for (var i = 1; i < 8; i++)
        {
            var delayIndex = i;
            Timer.Spawn(TimeSpan.FromSeconds(delayIndex * 0.25), () =>
            {
                if (Deleted(uid))
                    return;
                _heretic.SpawnOrbitingBlade(uid);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/unsheath.ogg"), uid, AudioParams.Default.WithVolume(-9.6f));
            });
        }
        foreach (var action in _actions.GetActions(uid))
        {
            if (!TryComp<WorldTargetActionComponent>(action.Owner, out var wta))
                continue;
            if (wta.Event is not HereticFuriousSteelActionEvent)
                continue;
            if (TryComp<ActionComponent>(action.Owner, out var ac) && ac.UseDelay.HasValue)
                _actions.SetUseDelay(new Entity<ActionComponent?>(action.Owner, ac), TimeSpan.FromSeconds(ac.UseDelay.Value.TotalSeconds * 0.5));
            break;
        }
        Spawn("HereticEffectCleave", coords);
        _popup.PopupEntity(Loc.GetString("heretic-ascension-blade"), uid, uid, PopupType.Large);
    }
}
