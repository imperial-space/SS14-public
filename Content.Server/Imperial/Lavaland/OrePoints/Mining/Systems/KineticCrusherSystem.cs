using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Projectiles;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Handles KineticCrusher mark, backstab mechanics, upgrade damage effects and target whitelist.
/// </summary>
public sealed class KineticCrusherSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KineticCrusherComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnCrusherProjectileHit);
        SubscribeLocalEvent<KineticMiningBulletComponent, ProjectileHitEvent>(OnMiningBulletHit);
    }

    private void OnMeleeHit(Entity<KineticCrusherComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        // ── Whitelist: full damage only to LavalandMob; everything else gets 1 Blunt ──
        // HitEntities is IReadOnlyList but the backing object is List<EntityUid> — cast to mutate.
        if (ent.Comp.TargetWhitelist != null && args.HitEntities is List<EntityUid> mutableHits)
        {
            // Collect non-whitelisted entities before mutating the list
            var notAllowed = new List<EntityUid>();
            foreach (var uid in mutableHits)
            {
                if (!_whitelist.IsValid(ent.Comp.TargetWhitelist, uid))
                    notAllowed.Add(uid);
            }

            // Remove them from the hit list so MeleeWeaponSystem won't deal base damage
            foreach (var uid in notAllowed)
                mutableHits.Remove(uid);

            // Apply exactly 1 Blunt (armor-piercing) to non-whitelisted damageable entities
            if (notAllowed.Count > 0)
            {
                var oneDmg = new DamageSpecifier();
                oneDmg.DamageDict["Blunt"] = 1;
                foreach (var uid in notAllowed)
                {
                    if (HasComp<DamageableComponent>(uid))
                        _damageable.TryChangeDamage(uid, oneDmg, ignoreResistances: true, interruptsDoAfters: false, origin: args.User);
                }
            }

            if (mutableHits.Count == 0)
            {
                // Light (click) attacks apply damage to target.Value independently of HitEntities.
                // Setting Handled stops the melee system from dealing base damage on top of our 1 Blunt.
                args.Handled = true;
                return;
            }
        }

        // Collect upgrade entities once per swing
        var upgrades = GetUpgrades(ent.Owner);

        foreach (var target in args.HitEntities)
        {
            var bonus = new DamageSpecifier();
            var pierceBonus = new DamageSpecifier(); // applied separately with ignoreResistances

            // ── Existing: mark bonus ──────────────────────────────────────
            var alreadyMarked = HasComp<MarkedByKineticCrusherComponent>(target);
            if (alreadyMarked)
                bonus += ent.Comp.MarkDamageBonus;

            // ── Existing: backstab bonus ──────────────────────────────────
            if (IsBackstab(args.User, target))
                bonus += ent.Comp.BackstabDamageBonus;

            // ── Upgrade: Щупальце Голиафа ─────────────────────────────────
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherGoliathTentacleUpgradeComponent>(upgrade, out var goliath))
                    continue;

                var wielderHealthFraction = GetDamageFraction(args.User);
                if (wielderHealthFraction > 0f)
                    bonus += goliath.MaxBonusDamage * wielderHealthFraction;

                break;
            }

            // ── Upgrade: Большое глазастое щупальце ──────────────────────
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherAncientGoliathTentacleUpgradeComponent>(upgrade, out var ancient))
                    continue;

                var targetDamageFraction = GetDamageFraction(target);
                var targetHealthFraction = 1f - targetDamageFraction;

                var tagOk = ancient.RequiredTag == null || _tag.HasTag(target, ancient.RequiredTag);

                if (tagOk && targetHealthFraction > ancient.HealthThreshold)
                {
                    if (TryComp<MeleeWeaponComponent>(ent, out var melee))
                        bonus += melee.Damage * ancient.BonusMultiplier;
                }

                break;
            }

            // ── Upgrade: Щупальце голиафа (первый удар) — Хвостовой шип ──
            // Bonus damage when target does NOT yet have the crusher mark.
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherTailSpikeUpgradeComponent>(upgrade, out var spike))
                    continue;

                if (!alreadyMarked)
                    bonus += spike.BonusDamage;

                break;
            }

            // ── Upgrade: Бластерные трубы (радиация по меченым) ──────────
            // Applied directly (ignoreResistances: true) — bypasses modifier sets so
            // radiation always lands even if the mob has a 0.0 Radiation coefficient.
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherBlasterTubesUpgradeComponent>(upgrade, out var tubes))
                    continue;

                if (alreadyMarked && !tubes.BonusDamage.Empty)
                    _damageable.TryChangeDamage(target, tubes.BonusDamage, ignoreResistances: true, interruptsDoAfters: false, origin: args.User);

                break;
            }

            // ── Upgrade: Коготь демона (лайфстил) ────────────────────────
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherDemonClawsUpgradeComponent>(upgrade, out var demon))
                    continue;

                var tagOk = demon.TargetTag == null || _tag.HasTag(target, demon.TargetTag);
                if (tagOk)
                {
                    var heal = new DamageSpecifier();
                    heal.DamageDict["Blunt"] = -demon.HealAmount;
                    _damageable.TryChangeDamage(args.User, heal, ignoreResistances: true, interruptsDoAfters: false);
                }

                break;
            }

            // ── Upgrade: Осколок расщелины (пробитие брони) ──────────────
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherCreviceShardUpgradeComponent>(upgrade, out var shard))
                    continue;

                pierceBonus += shard.PiercingBonus;
                break;
            }

            // ── Upgrade: Глаз охотника (метка в области) ─────────────────
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherHunterEyeUpgradeComponent>(upgrade, out var eye))
                    continue;

                MarkNearbyTargets(target, args.User, eye.MarkRadius, ent.Comp.MarkDuration);
                break;
            }

            // ── Upgrade: Вихревой талисман (отбрасывание) ────────────────
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherVortexTalismanUpgradeComponent>(upgrade, out var vortex))
                    continue;

                var userPos = _transform.GetWorldPosition(args.User);
                var targetPos = _transform.GetWorldPosition(target);
                var dir = targetPos - userPos;
                if (dir != Vector2.Zero)
                    _throwing.TryThrow(target, dir.Normalized() * vortex.KnockbackDistance, vortex.KnockbackStrength, args.User);

                break;
            }

            // ── Upgrade: Цепь (замедление) ───────────────────────────────
            foreach (var upgrade in upgrades)
            {
                if (!TryComp<CrusherChainUpgradeComponent>(upgrade, out var chain))
                    continue;

                _movementMod.TryAddMovementSpeedModDuration(
                    target,
                    MovementModStatusSystem.TaserSlowdown,
                    chain.SlowDuration,
                    chain.WalkSpeedModifier,
                    chain.SprintSpeedModifier);

                break;
            }

            // ── Apply normal bonus damage ─────────────────────────────────
            if (bonus.GetTotal() > FixedPoint2.Zero)
                _damageable.TryChangeDamage(target, bonus, ignoreResistances: false, origin: args.User);

            // ── Apply armor-piercing bonus ────────────────────────────────
            if (pierceBonus.GetTotal() > FixedPoint2.Zero)
                _damageable.TryChangeDamage(target, pierceBonus, ignoreResistances: true, origin: args.User);

            // ── Apply / refresh mark ──────────────────────────────────────
            var mark = EnsureComp<MarkedByKineticCrusherComponent>(target);
            mark.ExpiresAt = _timing.CurTime + ent.Comp.MarkDuration;
            Dirty(target, mark);
        }
    }

    // ─── Mining bullet whitelist ──────────────────────────────────────────────

    private void OnMiningBulletHit(Entity<KineticMiningBulletComponent> bullet, ref ProjectileHitEvent args)
    {
        if (bullet.Comp.TargetWhitelist == null)
            return;

        if (_whitelist.IsValid(bullet.Comp.TargetWhitelist, args.Target))
            return;

        args.Damage = bullet.Comp.FallbackDamage;
    }

    // ─── Projectile hit (mark shot) ──────────────────────────────────────────

    private void OnCrusherProjectileHit(Entity<ProjectileComponent> projectile, ref ProjectileHitEvent args)
    {
        // Only handle projectiles fired from a KineticCrusher weapon
        if (projectile.Comp.Weapon is not { } weaponUid)
            return;
        if (!TryComp<KineticCrusherComponent>(weaponUid, out var crusher))
            return;

        // Always mark the direct hit target
        var mark = EnsureComp<MarkedByKineticCrusherComponent>(args.Target);
        mark.ExpiresAt = _timing.CurTime + crusher.MarkDuration;
        Dirty(args.Target, mark);

        // Hunter Eye: mark all nearby damageable entities too
        var upgrades = GetUpgrades(weaponUid);
        foreach (var upgrade in upgrades)
        {
            if (!TryComp<CrusherHunterEyeUpgradeComponent>(upgrade, out var eye))
                continue;

            if (projectile.Comp.Shooter is { } shooter)
                MarkNearbyTargets(args.Target, shooter, eye.MarkRadius, crusher.MarkDuration);

            break;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks all damageable entities within <paramref name="radius"/> tiles of <paramref name="center"/>,
    /// excluding only the crusher wielder. The center target is included so the popup fires
    /// even when there is only one enemy in range.
    /// Shows a popup to the wielder listing how many entities were marked.
    /// </summary>
    private void MarkNearbyTargets(EntityUid center, EntityUid wielder, float radius, TimeSpan markDuration)
    {
        var coords = Transform(center).Coordinates;
        var nearby = _lookup.GetEntitiesInRange<DamageableComponent>(coords, radius);
        var marked = 0;
        foreach (var (uid, _) in nearby)
        {
            if (uid == wielder)
                continue;

            var mark = EnsureComp<MarkedByKineticCrusherComponent>(uid);
            mark.ExpiresAt = _timing.CurTime + markDuration;
            Dirty(uid, mark);
            marked++;
        }

        if (marked > 0)
            _popup.PopupEntity(Loc.GetString("crusher-hunter-eye-marked", ("count", marked)), wielder, wielder, PopupType.Medium);
    }

    /// <summary>
    /// Returns a list of upgrade entities installed in the crusher's upgrade container.
    /// Returns an empty list if the crusher has no <see cref="KineticCrusherUpgradeableComponent"/>.
    /// </summary>
    private List<EntityUid> GetUpgrades(EntityUid crusher)
    {
        var result = new List<EntityUid>();

        if (!TryComp<KineticCrusherUpgradeableComponent>(crusher, out var upgradeable))
            return result;

        if (!_container.TryGetContainer(crusher, upgradeable.UpgradesContainerId, out var upgradeContainer))
            return result;

        result.AddRange(upgradeContainer.ContainedEntities);
        return result;
    }

    /// <summary>
    /// Returns what fraction of max HP the entity has lost (0 = full HP, 1 = at death threshold).
    /// Returns 0 if the entity has no health components.
    /// </summary>
    private float GetDamageFraction(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable)
            || !TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return 0f;

        FixedPoint2 deadThreshold = 0;
        foreach (var (dmg, state) in thresholds.Thresholds)
        {
            if (state == MobState.Dead)
            {
                deadThreshold = dmg;
                break;
            }
        }

        if (deadThreshold <= 0)
            return 0f;

#pragma warning disable CS0618
        var totalDamage = _damageable.GetTotalDamage((uid, damageable));
#pragma warning restore CS0618
        return (float)(totalDamage / deadThreshold);
    }

    private bool IsBackstab(EntityUid attacker, EntityUid target)
    {
        var targetRot = _transform.GetWorldRotation(target);
        var attackerPos = _transform.GetWorldPosition(attacker);
        var targetPos = _transform.GetWorldPosition(target);

        var targetToAttacker = Vector2.Normalize(attackerPos - targetPos);
        var targetFacing = targetRot.ToWorldVec();

        return Vector2.Dot(targetFacing, targetToAttacker) < -0.5f;
    }

    // ─── Mark expiry ─────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MarkedByKineticCrusherComponent>();
        while (query.MoveNext(out var uid, out var mark))
        {
            if (now >= mark.ExpiresAt)
                RemCompDeferred<MarkedByKineticCrusherComponent>(uid);
        }
    }
}
