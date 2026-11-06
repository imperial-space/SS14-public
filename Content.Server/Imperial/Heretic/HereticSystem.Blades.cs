using Content.Server.Atmos.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Follower.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Ash;
using Content.Shared.Imperial.Heretic.Paths.Blade;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Content.Shared.Imperial.Heretic.Paths.Flesh;
using Content.Shared.Imperial.Heretic.Paths.Lock;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Клинки еретика и орбитальные клинки.
/// </summary>
public sealed partial class HereticSystem
{
    private static readonly ProtoId<DamageTypePrototype> HeatDamageType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> BloodlossDamageType = "Bloodloss";
    private static readonly ProtoId<DamageTypePrototype> SlashDamageType = "Slash";
    private static readonly ProtoId<DamageTypePrototype> PiercingDamageType = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> ColdDamageType = "Cold";
    private static readonly ProtoId<DamageTypePrototype> CausticDamageType = "Caustic";
    private static readonly ProtoId<DamageTypePrototype> RadiationDamageType = "Radiation";

    private static readonly EntProtoId HereticOrbitingBlade = "HereticOrbitingBlade";
    private static readonly EntProtoId HereticBladeFlesh = "HereticBladeFlesh";
    private static readonly EntProtoId HereticBladeCosmos = "HereticBladeCosmos";

    public void IncrementSunderedBladeCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return;
        comp.SunderedBladeCraftCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftSunderedBlade(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return false;
        return comp.SunderedBladeCraftCount < 5;
    }

    public void IncrementKeyBladeCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return;
        comp.KeyBladeCraftCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftKeyBlade(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return false;
        return comp.KeyBladeCraftCount < 2;
    }

    public void IncrementShatteredGhoulCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return;
        comp.ShatteredGhoulCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftShatteredGhoul(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return false;
        return comp.ShatteredGhoulCount < 1;
    }

    public EntityUid? SpawnOrbitingBlade(EntityUid uid, HereticComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return null;

        var blade = Spawn(HereticOrbitingBlade, Transform(uid).Coordinates);
        _xform.SetParent(blade, uid);
        var orbitComp = EnsureComp<HereticOrbitingBladeComponent>(blade);
        orbitComp.OwnerHeretic = uid;
        EnsureComp<OrbitVisualsComponent>(blade);
        comp.OrbitingBlades.Add(blade);
        comp.BladesCreated++;
        Dirty(uid, comp);
        return blade;
    }

    public bool TryConsumeOrbitingBlade(EntityUid uid, HereticComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        while (comp.OrbitingBlades.Count > 0)
        {
            var blade = comp.OrbitingBlades[^1];
            comp.OrbitingBlades.RemoveAt(comp.OrbitingBlades.Count - 1);
            if (!Exists(blade))
                continue;

            Del(blade);
            Dirty(uid, comp);
            return true;
        }

        return false;
    }

    public void ClearOrbitingBlades(EntityUid uid, HereticComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        foreach (var blade in comp.OrbitingBlades)
        {
            if (Exists(blade))
                Del(blade);
        }

        comp.OrbitingBlades.Clear();
        Dirty(uid, comp);
    }

    public void UpgradeBlade(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return;
        if (comp.CurrentPath == HereticPath.General)
            return;
        ApplyBladeUpgrade(uid, comp);
        comp.BladeUpgraded = true;
        Dirty(uid, comp);
    }

    private void SpawnBlade(EntityUid uid, HereticComponent comp, string bladeEntityId)
    {
        if (Exists(comp.CurrentBlade))
            Del(comp.CurrentBlade.Value);

        comp.CurrentBlade = null;
        var blade = Spawn(bladeEntityId, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(uid, blade);
        comp.CurrentBlade = blade;
        Dirty(uid, comp);
    }

    private void AddPathBladeComponent(EntityUid uid, HereticComponent comp, EntityUid blade)
    {
        if (!Exists(blade))
            return;
        switch (comp.CurrentPath)
        {
            case HereticPath.Ash: EnsureComp<HereticAshBladeComponent>(blade); break;
            case HereticPath.Lock: EnsureComp<HereticLockBladeComponent>(blade); break;
            case HereticPath.Void: EnsureComp<HereticVoidBladeComponent>(blade); break;
            case HereticPath.Blade: EnsureComp<HereticBladeBladeComponent>(blade); break;
            case HereticPath.Rust: EnsureComp<HereticRustBladeComponent>(blade); break;
            case HereticPath.Cosmos: EnsureComp<HereticCosmosBladeComponent>(blade); break;
            case HereticPath.Flesh: EnsureComp<HereticFleshBladeComponent>(blade); break;
            case HereticPath.Moon: EnsureComp<HereticMoonBladeComponent>(blade); break;
        }
    }

    private EntityUid? FindHeldPathBlade(EntityUid uid, HereticPath path)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            var isPathBlade = path switch
            {
                HereticPath.Ash => HasComp<HereticAshBladeComponent>(held),
                HereticPath.Lock => HasComp<HereticLockBladeComponent>(held),
                HereticPath.Void => HasComp<HereticVoidBladeComponent>(held),
                HereticPath.Blade => HasComp<HereticBladeBladeComponent>(held),
                HereticPath.Rust => HasComp<HereticRustBladeComponent>(held),
                HereticPath.Cosmos => HasComp<HereticCosmosBladeComponent>(held),
                HereticPath.Flesh => HasComp<HereticFleshBladeComponent>(held),
                HereticPath.Moon => HasComp<HereticMoonBladeComponent>(held),
                _ => false,
            };
            if (isPathBlade)
                return held;
        }
        return null;
    }

    private void ApplyBladeUpgrade(EntityUid uid, HereticComponent comp)
    {
        var found = Exists(comp.CurrentBlade) ? comp.CurrentBlade : null;
        if (found == null)
        {
            found = FindHeldPathBlade(uid, comp.CurrentPath);
            if (found == null)
            {
                // Fallback: find any held item with the HereticBlade tag (e.g. HereticBladeBase from ritual)
                foreach (var held in _hands.EnumerateHeld(uid))
                {
                    if (_tag.HasTag(held, HereticBladeTag))
                    {
                        found = held;
                        break;
                    }
                }

                if (found is not { } fallback)
                    return;

                AddPathBladeComponent(uid, comp, fallback);
            }

            comp.CurrentBlade = found;
            Dirty(uid, comp);
        }

        if (found is not { } blade)
            return;

        if (!TryComp<MeleeWeaponComponent>(blade, out var melee))
            return;

        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
                melee.Damage.DamageDict[HeatDamageType] = FixedPoint2.New(12);
                var ignite = EnsureComp<IgniteOnMeleeHitComponent>(blade);
                ignite.FireStacks = 1.0f;
                break;
            case HereticPath.Lock:
                melee.Damage.DamageDict[BluntDamageType] = FixedPoint2.New(10);
                melee.Damage.DamageDict[BloodlossDamageType] = FixedPoint2.New(5);
                break;
            case HereticPath.Flesh:
                {
                    var fleshPos = Transform(blade).Coordinates;
                    var fleshBlade = Spawn(HereticBladeFlesh, fleshPos);
                    EnsureComp<HereticFleshBladeComponent>(fleshBlade);
                    Del(blade);
                    comp.CurrentBlade = fleshBlade;
                    Dirty(uid, comp);
                    _hands.TryPickupAnyHand(uid, fleshBlade);
                    if (TryComp<MeleeWeaponComponent>(fleshBlade, out var fleshMelee))
                    {
                        fleshMelee.Damage.DamageDict[SlashDamageType] = FixedPoint2.New(22);
                        fleshMelee.Damage.DamageDict[PiercingDamageType] = FixedPoint2.New(8);
                        fleshMelee.Damage.DamageDict[BloodlossDamageType] = FixedPoint2.New(8);
                        Dirty(fleshBlade, fleshMelee);
                    }
                    return;
                }
            case HereticPath.Void:
                melee.Damage.DamageDict[ColdDamageType] = FixedPoint2.New(12);
                break;
            case HereticPath.Blade:
                melee.Damage.DamageDict[SlashDamageType] = FixedPoint2.New(28);
                melee.AttackRate = 2.0f;
                break;
            case HereticPath.Rust:
                melee.Damage.DamageDict[CausticDamageType] = FixedPoint2.New(12);
                break;
            case HereticPath.Cosmos:
                {
                    var cosmosPos = Transform(blade).Coordinates;
                    var cosmosBlade = Spawn(HereticBladeCosmos, cosmosPos);
                    EnsureComp<HereticCosmosBladeComponent>(cosmosBlade);
                    Del(blade);
                    comp.CurrentBlade = cosmosBlade;
                    Dirty(uid, comp);
                    _hands.TryPickupAnyHand(uid, cosmosBlade);
                    if (TryComp<MeleeWeaponComponent>(cosmosBlade, out var cosmelee))
                    {
                        cosmelee.Damage.DamageDict[RadiationDamageType] = FixedPoint2.New(12);
                        Dirty(cosmosBlade, cosmelee);
                    }
                    return;
                }
        }

        Dirty(blade, melee);
    }

}
