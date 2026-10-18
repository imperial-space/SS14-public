using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Server.Imperial.Lavaland.MegafaunaTracker;
using Content.Server.Imperial.Lavaland.Resonator;
using Content.Server.Imperial.Lavaland.Storm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// projectile/kinetic и модули акселератора: штраф давления, взрывы, повторитель, вампиризм,
/// резонанс и «сифон смерти».
/// </summary>
public sealed class KineticAcceleratorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GatherableSystem _gather = default!;
    [Dependency] private readonly KineticEquipmentSystem _equipment = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ResonatorSystem _resonator = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> LavalandMob = "LavalandMob";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageGroupPrototype>[] HealOrder = { "Brute", "Burn", "Airloss" };
    private static readonly EntProtoId ExplosionEffect = "KineticExplosionFastEffect";
    /// <summary>status_effect/syphon_mark duration.</summary>
    private static readonly TimeSpan SyphonDuration = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KineticAcceleratorComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<KineticProjectileComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<KineticProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<KineticProjectileComponent, TimedDespawnEvent>(OnRangeEnd);
        SubscribeLocalEvent<KineticSyphonMarkComponent, MobStateChangedEvent>(OnSyphonTargetDied);
    }

    #region Выстрел

    /// <summary>modify_projectile(): модули меняют дальность, урон и штраф давления.</summary>
    private void OnGunShot(Entity<KineticAcceleratorComponent> ent, ref GunShotEvent args)
    {
        var modkits = _equipment.GetModkits(ent).ToList();
        var range = ent.Comp.Range + modkits.Sum(m => m.Comp.Range);
        var damage = modkits.Sum(m => m.Comp.Damage);
        var pressure = 1f;
        foreach (var modkit in modkits)
        {
            pressure *= modkit.Comp.PressureMultiplier;
        }

        var speed = TryComp<GunComponent>(ent, out var gun) ? gun.ProjectileSpeedModified : 25f;

        foreach (var (uid, _) in args.Ammo)
        {
            if (uid is not { } projectile || !TryComp<KineticProjectileComponent>(projectile, out var kinetic))
                continue;

            kinetic.Gun = ent;
            kinetic.Firer = args.User;
            kinetic.PressureDecrease *= pressure;

            if (TryComp<TimedDespawnComponent>(projectile, out var despawn) && speed > 0f)
                despawn.Lifetime = range / speed;

            if (damage > 0f && TryComp<ProjectileComponent>(projectile, out var proj))
                proj.Damage += new DamageSpecifier { DamageDict = { [Blunt] = damage } };
        }
    }

    #endregion

    #region Попадание

    /// <summary>human_passthrough / ignored_mob_types.</summary>
    private void OnPreventCollide(Entity<KineticProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !HasComp<HumanoidProfileComponent>(args.OtherEntity))
            return;

        if (GetModkits(ent).Any(m => m.Comp.HumanPassthrough))
            args.Cancelled = true;
    }

    /// <summary>prehit_pierce() + on_hit() → strike_thing().</summary>
    private void OnProjectileHit(Entity<KineticProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (ent.Comp.Struck)
            return;

        var modkits = GetModkits(ent);
        var target = args.Target;

        // projectile_prehit(): до штрафа давления.
        foreach (var modkit in modkits)
        {
            if (modkit.Comp.Lifesteal > 0f && IsAliveMob(target) && ent.Comp.Firer is { } firer)
                HealOrdered(firer, modkit.Comp.Lifesteal);

            if (modkit.Comp.Bounty && IsAliveMob(target))
            {
                var mark = EnsureComp<KineticSyphonMarkComponent>(target);
                mark.Modkit = modkit;
                mark.ExpiresAt = _timing.CurTime + SyphonDuration;
            }
        }

        if (!ent.Comp.PressureDecreaseActive && !IsLowPressure(ent, target))
        {
            ent.Comp.PressureDecreaseActive = true;
            args.Damage *= ent.Comp.PressureDecrease;
        }

        Strike(ent, modkits, Transform(target).Coordinates, target, args.Damage.GetTotal().Float());
    }

    /// <summary>on_range(): снаряд долетел до конца дальности.</summary>
    private void OnRangeEnd(Entity<KineticProjectileComponent> ent, ref TimedDespawnEvent args)
    {
        if (ent.Comp.Struck)
            return;

        var damage = TryComp<ProjectileComponent>(ent, out var proj) ? proj.Damage.GetTotal().Float() : 0f;
        if (!ent.Comp.PressureDecreaseActive && !IsLowPressure(ent, null))
        {
            ent.Comp.PressureDecreaseActive = true;
            damage *= ent.Comp.PressureDecrease;
        }

        Strike(ent, GetModkits(ent), Transform(ent).Coordinates, null, damage);
    }

    /// <summary>strike_thing().</summary>
    private void Strike(Entity<KineticProjectileComponent> ent, List<Entity<KineticModkitComponent>> modkits, EntityCoordinates coords, EntityUid? target, float damage)
    {
        ent.Comp.Struck = true;
        var mineral = target != null && HasComp<GatherableComponent>(target);

        // projectile_strike_predamage(): rapid repeater.
        foreach (var modkit in modkits)
        {
            if (!modkit.Comp.Repeater || ent.Comp.Gun is not { } gunUid)
                continue;

            if (mineral || target != null && IsAliveMob(target.Value))
                QuickReload(gunUid, 0.25f);
        }

        // projectile_strike().
        foreach (var modkit in modkits)
        {
            if (modkit.Comp.Aoe)
                AoeStrike(ent, modkit, coords, target, damage);

            if (modkit.Comp.ResonatorBlasts && !mineral)
                _resonator.KineticResonance(coords, ent.Comp.Firer, modkit.Comp.ResonatorMultiplier);

            if (modkit.Comp.Bounty && target is { } living && HasComp<MobStateComponent>(living) &&
                MetaData(living).EntityPrototype?.ID is { } proto && modkit.Comp.Bounties.TryGetValue(proto, out var bonus))
            {
                if (ent.Comp.PressureDecreaseActive)
                    bonus *= ent.Comp.PressureDecrease;
                _damageable.TryChangeDamage(living, new DamageSpecifier { DamageDict = { [Blunt] = bonus } }, origin: ent.Comp.Firer);
            }
        }
    }

    /// <summary>cooldown/aoe/projectile_strike().</summary>
    private void AoeStrike(Entity<KineticProjectileComponent> ent, Entity<KineticModkitComponent> modkit, EntityCoordinates coords, EntityUid? target, float damage)
    {
        Spawn(ExplosionEffect, coords);

        if (modkit.Comp.AoeTurfs)
        {
            foreach (var (rock, gatherable) in _lookup.GetEntitiesInRange<GatherableComponent>(coords, 1.5f).ToList())
            {
                if (rock != target && !TerminatingOrDeleted(rock))
                    _gather.Gather(rock, ent.Comp.Firer, gatherable);
            }
        }

        if (modkit.Comp.AoeMobDamage <= 0f)
            return;

        var amount = damage * modkit.Comp.AoeMobDamage;
        foreach (var (mob, _) in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 1.5f).ToList())
        {
            if (mob == target || mob == ent.Comp.Firer || !IsMiningMob(mob))
                continue;

            _damageable.TryChangeDamage(mob, new DamageSpecifier { DamageDict = { [Blunt] = amount } }, origin: ent.Comp.Firer);
            _popup.PopupEntity(Loc.GetString("kinetic-explosion-hit"), mob, mob, PopupType.LargeCaution);
        }
    }

    #endregion

    #region Сифон смерти

    private void OnSyphonTargetDied(Entity<KineticSyphonMarkComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (_timing.CurTime <= ent.Comp.ExpiresAt && TryComp<KineticModkitComponent>(ent.Comp.Modkit, out var modkit) &&
            MetaData(ent).EntityPrototype?.ID is { } proto)
        {
            // get_kill(): мегафауна даёт вчетверо больше.
            var bonus = modkit.BountyModifier * (IsMegafauna(ent) ? 4f : 1f);
            modkit.Bounties[proto] = Math.Min((modkit.Bounties.GetValueOrDefault(proto)) + bonus, modkit.BountyMax);
        }

        RemCompDeferred<KineticSyphonMarkComponent>(ent);
    }

    #endregion

    #region Помощники

    private List<Entity<KineticModkitComponent>> GetModkits(Entity<KineticProjectileComponent> ent)
    {
        if (ent.Comp.Gun is not { } gun || !TryComp<KineticAcceleratorComponent>(gun, out var accelerator))
            return new List<Entity<KineticModkitComponent>>();

        return _equipment.GetModkits((gun, accelerator)).ToList();
    }

    /// <summary>
    /// lavaland_equipment_pressure_check(): давление на клетке цели не выше 50 кПа
    /// (на Лаваленде — не выше давления его атмосферы).
    /// </summary>
    public bool IsLowPressure(Entity<KineticProjectileComponent> ent, EntityUid? target)
    {
        var where = target ?? ent.Owner;
        var mixture = _atmos.GetTileMixture(where) ?? _atmos.GetTileMixture(ent.Owner);
        if (mixture == null)
            return true;

        var threshold = ent.Comp.PressureThreshold;
        if (Transform(where).MapUid is { } map && HasComp<LavalandMapComponent>(map))
            threshold = Math.Max(threshold, ent.Comp.LavalandPressure);

        return mixture.Pressure <= threshold;
    }

    /// <summary>attempt_reload(recharge_time * multiplier).</summary>
    private void QuickReload(EntityUid gun, float multiplier)
    {
        if (!TryComp<RechargeBasicEntityAmmoComponent>(gun, out var recharge))
            return;

        recharge.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(recharge.RechargeCooldown * multiplier);
        Dirty(gun, recharge);
    }

    private bool IsAliveMob(EntityUid uid)
    {
        return TryComp<MobStateComponent>(uid, out var state) && !_mobState.IsDead(uid, state);
    }

    /// <summary>ismining(): существа Лаваленда.</summary>
    public bool IsMiningMob(EntityUid uid)
    {
        return _tag.HasTag(uid, LavalandMob) || IsMegafauna(uid);
    }

    private bool IsMegafauna(EntityUid uid)
    {
        return HasComp<LavalandMegafaunaComponent>(uid) || HasComp<Content.Server.Imperial.Lavaland.Megafauna.MegafaunaAiComponent>(uid);
    }

    /// <summary>heal_ordered_damage(): лечит урон по порядку групп.</summary>
    public void HealOrdered(EntityUid uid, float amount, ProtoId<DamageGroupPrototype>[]? order = null)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var remaining = FixedPoint2.New(amount);
        var perGroup = _damageable.GetDamagePerGroup((uid, damageable));
        foreach (var group in order ?? HealOrder)
        {
            if (remaining <= FixedPoint2.Zero)
                break;
            if (!perGroup.TryGetValue(group, out var have) || have <= FixedPoint2.Zero)
                continue;

            var heal = FixedPoint2.Min(have, remaining);
            _damageable.HealDistributed((uid, damageable), -heal, group);
            remaining -= heal;
        }
    }

    #endregion
}

/// <summary>status_effect/syphon_mark.</summary>
[RegisterComponent]
public sealed partial class KineticSyphonMarkComponent : Component
{
    [ViewVariables]
    public EntityUid Modkit;

    [ViewVariables]
    public TimeSpan ExpiresAt;
}
