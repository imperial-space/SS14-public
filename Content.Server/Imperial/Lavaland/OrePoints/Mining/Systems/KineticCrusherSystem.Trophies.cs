using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Трофеи дробителя из trophies_fauna.dm / trophies_megafauna.dm / trophies_misc.dm,
/// которым нужна отдельная логика: лобстер, бримдемон, желчный червь, ледяной демон, волк,
/// медведь, раптор, демонический шахтёр, мать-голиаф, легионер и «Нечто».
/// </summary>
public sealed partial class KineticCrusherSystem
{
    [Dependency] private readonly CrusherStatusSystem _status = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly GatherableSystem _gather = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    private static readonly ProtoId<DamageGroupPrototype>[] FleshHealOrder = { "Brute", "Burn", "Airloss", "Toxin" };
    private static readonly string SummonFaction = "CrusherSummon";
    private static readonly EntProtoId BileProjectile = "CrusherBileProjectile";
    private static readonly EntProtoId DemonAfterimage = "MobCrusherDemonAfterimage";
    private static readonly EntProtoId LegionSkull = "MobCrusherLegionSkull";
    private static readonly EntProtoId TentacleEffect = "CrusherBroodTentacleEffect";
    private static readonly SoundSpecifier BrimdemonSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/crusher/brimdemon_crush.ogg");
    private static readonly SoundSpecifier BileSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/crusher/bileworm_spit.ogg");
    private static readonly SoundSpecifier ChimeSound = new SoundPathSpecifier("/Audio/Imperial/Lavaland/crusher/chime.ogg");
    private static readonly string[] ComicPhrases = { "BOOM", "BANG", "KABLOW", "KAPOW", "OUCH", "BAM", "KAPOW", "WHAM", "POW", "KABOOM" };
    private static readonly Vector2[] CardinalVectors = { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) };

    /// <summary>broodmother/patch: щупальца хватают через секунду после появления.</summary>
    private readonly List<(TimeSpan At, MapCoordinates Coords, EntityUid User)> _tentacles = new();

    private void InitializeTrophies()
    {
        SubscribeLocalEvent<CrusherDestabilizerComponent, PreventCollideEvent>(OnDestabilizerPreventCollide);
        SubscribeLocalEvent<CrusherBileComponent, PreventCollideEvent>(OnBilePreventCollide);
        SubscribeLocalEvent<CrusherTrophyComponent, UseInHandEvent>(OnTrophyUseInHand);
        SubscribeLocalEvent<CrusherRebukedComponent, GetMeleeAttackRateEvent>(OnRebukedAttackRate);
        SubscribeLocalEvent<CrusherRebukedComponent, GunRefreshModifiersEvent>(OnRebukedGun);
        SubscribeLocalEvent<CrusherRebukedComponent, ComponentShutdown>(OnRebukedShutdown);
    }

    #region on_mark_detonation

    private void OnMarkDetonationExtra(Entity<KineticCrusherComponent> crusher, Entity<CrusherTrophyComponent> trophy, EntityUid user, EntityUid target)
    {
        var bonus = trophy.Comp.BonusValue;
        var now = _timing.CurTime;
        switch (trophy.Comp.Trophy)
        {
            // lobster_claw: rebuked на bonus_value секунд.
            case CrusherTrophyKind.LobsterClaw:
                var rebuked = EnsureComp<CrusherRebukedComponent>(target);
                rebuked.Until = now + TimeSpan.FromSeconds(bonus);
                if (HasComp<GunComponent>(target))
                    _gun.RefreshModifiers(target);
                break;

            // brimdemon_fang: комиксная надпись и звук.
            case CrusherTrophyKind.BrimdemonFang:
                _popup.PopupCoordinates($"{_random.Pick(ComicPhrases)}!", Transform(target).Coordinates, PopupType.LargeCaution);
                _audio.PlayPvs(BrimdemonSound, target);
                break;

            // bileworm_spewlet: dir_shots в четыре стороны раз в 10 секунд.
            case CrusherTrophyKind.BilewormSpewlet:
                if (now < trophy.Comp.CooldownEnd)
                    break;
                trophy.Comp.CooldownEnd = now + TimeSpan.FromSeconds(10);
                FireBile(user);
                break;

            // ice_demon_cube: два клона вокруг цели раз в 30 секунд.
            case CrusherTrophyKind.IceDemonCube:
                if (now < trophy.Comp.CooldownEnd)
                    break;
                trophy.Comp.CooldownEnd = now + TimeSpan.FromSeconds(30);
                for (var i = 0; i < 2; i++)
                {
                    Spawn(DemonAfterimage, DropOffNear(target, user));
                }
                break;

            // wolf_ear: speed_boost на 1 секунду.
            case CrusherTrophyKind.WolfEar:
                _status.SpeedBoost(user, TimeSpan.FromSeconds(1));
                _audio.PlayPvs(ChimeSound, user, AudioParams.Default.WithVolume(-5f));
                break;

            // bear_paw: при здоровье ниже половины — второй удар через 0.1 с.
            case CrusherTrophyKind.BearPaw:
                if (HealthFraction(user) <= 0.5f)
                {
                    var crusherUid = crusher.Owner;
                    Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(0.1), () => SecondStrike(crusherUid, user, target));
                }
                break;

            // ice_block_talisman: ледяная глыба на 4 секунды.
            case CrusherTrophyKind.IceBlockTalisman:
                _status.Freeze(target, TimeSpan.FromSeconds(4));
                _popup.PopupEntity(Loc.GetString("crusher-trophy-frozen"), target, target, PopupType.LargeCaution);
                break;

            // broodmother_tongue: bonus_value% шанс поля щупалец под целью.
            case CrusherTrophyKind.BroodmotherTongue:
                if (_random.Prob(bonus / 100f) && !_mobState.IsDead(target))
                    TentaclePatch(Transform(target).Coordinates, user);
                break;

            // legionnaire_spine: bonus_value% шанс черепа-союзника.
            case CrusherTrophyKind.LegionnaireSpine:
                if (_random.Prob(bonus / 100f) && !_mobState.IsDead(target))
                    Spawn(LegionSkull, Transform(user).Coordinates);
                break;

            // flesh_glob: лечит bonus_value * 0.5.
            case CrusherTrophyKind.FleshGlob:
                _accelerator.HealOrdered(user, bonus * 0.5f, FleshHealOrder);
                break;
        }
    }

    /// <summary>Доля оставшегося здоровья (health / maxHealth, где maxHealth — порог крита).</summary>
    private float HealthFraction(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable) ||
            !_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit) && !_thresholds.TryGetDeadThreshold(uid, out crit) ||
            crit is not { } max || max <= FixedPoint2.Zero)
            return 1f;

        return 1f - (_damageable.GetTotalDamage((uid, damageable)) / max).Float();
    }

    /// <summary>melee_attack_chain() повторного удара bear_paw.</summary>
    private void SecondStrike(EntityUid crusher, EntityUid user, EntityUid target)
    {
        if (TerminatingOrDeleted(crusher) || TerminatingOrDeleted(target) || TerminatingOrDeleted(user) ||
            !TryComp<KineticCrusherComponent>(crusher, out var comp) || !TryComp<MeleeWeaponComponent>(crusher, out var melee))
            return;

        var damage = _melee.GetDamage(crusher, user, melee);
        if (_damageable.TryChangeDamage(target, damage, out var dealt, origin: user))
            AddCrusherDamage(target, dealt.GetTotal().Float());
        if (melee.HitSound != null)
            _audio.PlayPvs(melee.HitSound, target);

        foreach (var trophy in _equipment.GetTrophies((crusher, comp)))
        {
            if (trophy.Comp.Trophy == CrusherTrophyKind.DemonClaws)
                _accelerator.HealOrdered(user, trophy.Comp.BonusValue * 0.1f, DemonHealOrder);
            else if (trophy.Comp.Trophy == CrusherTrophyKind.FleshGlob)
                _accelerator.HealOrdered(user, trophy.Comp.BonusValue * 0.2f, FleshHealOrder);
        }
    }

    /// <summary>find_dropoff_turf(): свободная клетка рядом с целью, иначе клетка игрока.</summary>
    private EntityCoordinates DropOffNear(EntityUid target, EntityUid user)
    {
        var xform = Transform(target);
        foreach (var dir in CardinalVectors.OrderBy(_ => _random.Next()))
        {
            var coords = xform.Coordinates.Offset(dir);
            if (!_lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.3f).Any())
                return coords;
        }

        return Transform(user).Coordinates;
    }

    #endregion

    #region Желчный червь

    /// <summary>projectile_attack/dir_shots/spewlet: четыре кислотных плевка.</summary>
    private void FireBile(EntityUid user)
    {
        var coords = _transform.GetMapCoordinates(user);
        _audio.PlayPvs(BileSound, user);
        foreach (var dir in CardinalVectors)
        {
            var bile = Spawn(BileProjectile, coords);
            EnsureComp<CrusherBileComponent>(bile).Firer = user;
            _gun.ShootProjectile(bile, dir, Vector2.Zero, null, user, 10f);
        }
    }

    private void OnBilePreventCollide(Entity<CrusherBileComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || ent.Comp.Firer is not { } firer)
            return;

        if (args.OtherEntity == firer || HasComp<MobStateComponent>(args.OtherEntity) && IsCrusherAlly(firer, args.OtherEntity))
            args.Cancelled = true;
    }

    /// <summary>bileworm_spewlet/on_projectile_hit_mineral(): бурит породу вокруг.</summary>
    private void OnDestabilizerHitMineral(Entity<CrusherDestabilizerComponent> ent, EntityUid target)
    {
        if (!HasComp<GatherableComponent>(target) || ent.Comp.Crusher is not { } crusherUid ||
            !TryComp<KineticCrusherComponent>(crusherUid, out var crusher) ||
            !_equipment.HasTrophy((crusherUid, crusher), CrusherTrophyKind.BilewormSpewlet))
            return;

        foreach (var (rock, gatherable) in _lookup.GetEntitiesInRange<GatherableComponent>(Transform(target).Coordinates, 1.5f).ToList())
        {
            if (rock != target && !TerminatingOrDeleted(rock))
                _gather.Gather(rock, ent.Comp.Firer, gatherable);
        }
    }

    #endregion

    #region Раптор

    /// <summary>projectile/destabilizer/prehit_pierce(): ignore_allies пропускает союзников.</summary>
    private void OnDestabilizerPreventCollide(Entity<CrusherDestabilizerComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !ent.Comp.IgnoreAllies || ent.Comp.Firer is not { } firer || !HasComp<MobStateComponent>(args.OtherEntity))
            return;

        if (IsCrusherAlly(firer, args.OtherEntity))
            args.Cancelled = true;
    }

    #endregion

    #region Лобстер

    /// <summary>rebuked: next_move_modifier *= 2.</summary>
    private void OnRebukedAttackRate(Entity<CrusherRebukedComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (ent.Comp.Until > _timing.CurTime)
            args.Multipliers *= 0.5f;
    }

    /// <summary>rebuked: ranged_cooldown_time *= 2.5.</summary>
    private void OnRebukedGun(Entity<CrusherRebukedComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.Until > _timing.CurTime && ent.Comp.LifeStage < ComponentLifeStage.Stopping)
            args.FireRate /= 2.5f;
    }

    private void OnRebukedShutdown(Entity<CrusherRebukedComponent> ent, ref ComponentShutdown args)
    {
        if (HasComp<GunComponent>(ent))
            _gun.RefreshModifiers(ent.Owner);
    }

    #endregion

    #region Мать-голиаф

    /// <summary>goliath_tentacle/broodmother/patch: 3×3 вокруг цели и ещё по клетке через одну в четыре стороны.</summary>
    private void TentaclePatch(EntityCoordinates center, EntityUid user)
    {
        var offsets = new List<Vector2>();
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                offsets.Add(new Vector2(x, y));
            }
        }

        offsets.AddRange(CardinalVectors.Select(d => d * 2));

        var now = _timing.CurTime;
        foreach (var offset in offsets)
        {
            var coords = center.Offset(offset);
            Spawn(TentacleEffect, coords);
            _tentacles.Add((now + TimeSpan.FromSeconds(1), _transform.ToMapCoordinates(coords), user));
        }
    }

    /// <summary>goliath_tentacle/grab(): 30–35 урона и захват на grapple_time.</summary>
    private void TentacleGrab(MapCoordinates coords, EntityUid user)
    {
        foreach (var (mob, _) in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.45f).ToList())
        {
            if (mob == user || _mobState.IsDead(mob) || IsCrusherAlly(user, mob))
                continue;

            _damageable.TryChangeDamage(mob, new DamageSpecifier { DamageDict = { [Blunt] = _random.Next(30, 36) } }, origin: user);
            _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(1));
        }
    }

    #endregion

    #region Использование в руке

    private void OnTrophyUseInHand(Entity<CrusherTrophyComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var now = _timing.CurTime;
        switch (ent.Comp.Trophy)
        {
            // broodmother_tongue/attack_self(): иммунитет к лаве на 10 секунд, раз в минуту.
            case CrusherTrophyKind.BroodmotherTongue:
                args.Handled = true;
                if (ent.Comp.UseCooldownEnd > now)
                {
                    _popup.PopupEntity(Loc.GetString("crusher-trophy-tongue-dry"), user, user);
                    return;
                }

                if (TryComp<CrusherLavaImmuneComponent>(user, out var existing) && existing.Until > now)
                {
                    _popup.PopupEntity(Loc.GetString("crusher-trophy-tongue-useless"), user, user);
                    return;
                }

                EnsureComp<CrusherLavaImmuneComponent>(user).Until = now + TimeSpan.FromSeconds(10);
                ent.Comp.UseCooldownEnd = now + TimeSpan.FromSeconds(60);
                _popup.PopupEntity(Loc.GetString("crusher-trophy-tongue-squeeze"), user, user);
                break;

            // legionnaire_spine/attack_self(): череп-союзник раз в 4 секунды.
            case CrusherTrophyKind.LegionnaireSpine:
                args.Handled = true;
                if (ent.Comp.UseCooldownEnd > now)
                {
                    _popup.PopupEntity(Loc.GetString("crusher-trophy-spine-wait"), user, user);
                    return;
                }

                ent.Comp.UseCooldownEnd = now + TimeSpan.FromSeconds(4);
                Spawn(LegionSkull, Transform(user).Coordinates);
                _popup.PopupEntity(Loc.GetString("crusher-trophy-spine-shake", ("user", user)), user, PopupType.MediumCaution);
                break;
        }
    }

    #endregion

    /// <summary>faction_check: союзники стрелка и призванные трофеями существа.</summary>
    private bool IsCrusherAlly(EntityUid user, EntityUid other)
    {
        return _faction.IsEntityFriendly(user, other) || _faction.IsMember(other, SummonFaction);
    }

    private void UpdateTrophies(TimeSpan now)
    {
        for (var i = _tentacles.Count - 1; i >= 0; i--)
        {
            var tentacle = _tentacles[i];
            if (now < tentacle.At)
                continue;
            _tentacles.RemoveAt(i);
            TentacleGrab(tentacle.Coords, tentacle.User);
        }

        var rebuked = EntityQueryEnumerator<CrusherRebukedComponent>();
        while (rebuked.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.Until)
                RemCompDeferred<CrusherRebukedComponent>(uid);
        }

        // TRAIT_LAVA_IMMUNE: лава в SS14 поджигает, поэтому иммунитет гасит огонь.
        var lava = EntityQueryEnumerator<CrusherLavaImmuneComponent>();
        while (lava.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.Until)
            {
                RemCompDeferred<CrusherLavaImmuneComponent>(uid);
                continue;
            }

            if (TryComp<FlammableComponent>(uid, out var flammable) && (flammable.OnFire || flammable.FireStacks > 0))
                _flammable.Extinguish(uid, flammable);
        }
    }
}

/// <summary>Кислотный плевок bileworm_spewlet.</summary>
[RegisterComponent]
public sealed partial class CrusherBileComponent : Component
{
    [ViewVariables]
    public EntityUid? Firer;
}

/// <summary>status_effect/rebuked.</summary>
[RegisterComponent]
public sealed partial class CrusherRebukedComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>TRAIT_LAVA_IMMUNE от broodmother_tongue.</summary>
[RegisterComponent]
public sealed partial class CrusherLavaImmuneComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}
