using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Decals;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Follower.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Ash;
using Content.Shared.Imperial.Heretic.Paths.Blade;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Content.Shared.Imperial.Heretic.Paths.Flesh;
using Content.Shared.Imperial.Heretic.Paths.Lock;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Клинки еретика и орбитальные клинки.
/// </summary>
public sealed partial class HereticSystem
{
    public void IncrementSunderedBladeCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        comp.SunderedBladeCraftCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftSunderedBlade(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return false;
        return comp.SunderedBladeCraftCount < 5;
    }

    public void IncrementKeyBladeCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        comp.KeyBladeCraftCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftKeyBlade(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return false;
        return comp.KeyBladeCraftCount < 2;
    }

    public void IncrementShatteredGhoulCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        comp.ShatteredGhoulCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftShatteredGhoul(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return false;
        return comp.ShatteredGhoulCount < 1;
    }


    public EntityUid SpawnOrbitingBlade(EntityUid uid, HereticComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return EntityUid.Invalid;

        var index = comp.OrbitingBlades.Count;
        var blade = Spawn("HereticOrbitingBlade", Transform(uid).Coordinates);
        _xform.SetParent(blade, uid);
        var orbitComp = EnsureComp<HereticOrbitingBladeComponent>(blade);
        orbitComp.OwnerHeretic = uid;
        var orbitVisuals = EnsureComp<OrbitVisualsComponent>(blade);
        orbitVisuals.PhaseOffset = index / 3f;
        Dirty(blade, orbitVisuals);
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
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        if (comp.CurrentPath == HereticPath.General) return;
        ApplyBladeUpgrade(uid, comp);
        comp.BladeUpgraded = true;
        Dirty(uid, comp);
    }

    private void SpawnBlade(EntityUid uid, HereticComponent comp, string bladeEntityId)
    {
        if (comp.CurrentBlade != EntityUid.Invalid && Exists(comp.CurrentBlade))
            Del(comp.CurrentBlade);

        comp.CurrentBlade = EntityUid.Invalid;
        var blade = Spawn(bladeEntityId, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(uid, blade);
        comp.CurrentBlade = blade;
        Dirty(uid, comp);
    }

    private void AddPathBladeComponent(EntityUid uid, HereticComponent comp, EntityUid blade)
    {
        if (!Exists(blade)) return;
        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:    EnsureComp<HereticAshBladeComponent>(blade); break;
            case HereticPath.Lock:   EnsureComp<HereticLockBladeComponent>(blade); break;
            case HereticPath.Void:   EnsureComp<HereticVoidBladeComponent>(blade); break;
            case HereticPath.Blade:  EnsureComp<HereticBladeBladeComponent>(blade); break;
            case HereticPath.Rust:   EnsureComp<HereticRustBladeComponent>(blade); break;
            case HereticPath.Cosmos: EnsureComp<HereticCosmosBladeComponent>(blade); break;
            case HereticPath.Flesh:  EnsureComp<HereticFleshBladeComponent>(blade); break;
            case HereticPath.Moon:   EnsureComp<HereticMoonBladeComponent>(blade); break;
        }
    }

    private EntityUid FindHeldPathBlade(EntityUid uid, HereticPath path)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            var isPathBlade = path switch
            {
                HereticPath.Ash    => HasComp<HereticAshBladeComponent>(held),
                HereticPath.Lock   => HasComp<HereticLockBladeComponent>(held),
                HereticPath.Void   => HasComp<HereticVoidBladeComponent>(held),
                HereticPath.Blade  => HasComp<HereticBladeBladeComponent>(held),
                HereticPath.Rust   => HasComp<HereticRustBladeComponent>(held),
                HereticPath.Cosmos => HasComp<HereticCosmosBladeComponent>(held),
                HereticPath.Flesh  => HasComp<HereticFleshBladeComponent>(held),
                HereticPath.Moon   => HasComp<HereticMoonBladeComponent>(held),
                _                  => false,
            };
            if (isPathBlade) return held;
        }
        return EntityUid.Invalid;
    }

    private void ApplyBladeUpgrade(EntityUid uid, HereticComponent comp)
    {
        EntityUid blade;
        if (comp.CurrentBlade != EntityUid.Invalid && Exists(comp.CurrentBlade))
        {
            blade = comp.CurrentBlade;
        }
        else
        {
            blade = FindHeldPathBlade(uid, comp.CurrentPath);
            if (blade == EntityUid.Invalid)
            {
                // Fallback: find any held item with the HereticBlade tag (e.g. HereticBladeBase from ritual)
                foreach (var held in _hands.EnumerateHeld(uid))
                {
                    if (_tag.HasTag(held, HereticBladeTag))
                    {
                        blade = held;
                        break;
                    }
                }
                if (blade == EntityUid.Invalid) return;
                AddPathBladeComponent(uid, comp, blade);
            }
            comp.CurrentBlade = blade;
            Dirty(uid, comp);
        }
        if (!TryComp<MeleeWeaponComponent>(blade, out var melee)) return;

        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
                melee.Damage.DamageDict["Heat"] = FixedPoint2.New(12);
                var ignite = EnsureComp<IgniteOnMeleeHitComponent>(blade);
                ignite.FireStacks = 1.0f;
                break;
            case HereticPath.Lock:
                melee.Damage.DamageDict["Blunt"] = FixedPoint2.New(10);
                melee.Damage.DamageDict["Bloodloss"] = FixedPoint2.New(5);
                break;
            case HereticPath.Flesh:
            {
                var fleshPos = Transform(blade).Coordinates;
                var fleshBlade = Spawn("HereticBladeFlesh", fleshPos);
                EnsureComp<HereticFleshBladeComponent>(fleshBlade);
                Del(blade);
                comp.CurrentBlade = fleshBlade;
                Dirty(uid, comp);
                _hands.TryPickupAnyHand(uid, fleshBlade);
                if (TryComp<MeleeWeaponComponent>(fleshBlade, out var fleshMelee))
                {
                    fleshMelee.Damage.DamageDict["Slash"] = FixedPoint2.New(22);
                    fleshMelee.Damage.DamageDict["Piercing"] = FixedPoint2.New(8);
                    fleshMelee.Damage.DamageDict["Bloodloss"] = FixedPoint2.New(8);
                    Dirty(fleshBlade, fleshMelee);
                }
                return;
            }
            case HereticPath.Void:
                melee.Damage.DamageDict["Cold"] = FixedPoint2.New(12);
                break;
            case HereticPath.Blade:
                melee.Damage.DamageDict["Slash"] = FixedPoint2.New(28);
                melee.AttackRate = 2.0f;
                break;
            case HereticPath.Rust:
                melee.Damage.DamageDict["Caustic"] = FixedPoint2.New(12);
                break;
            case HereticPath.Cosmos:
            {
                var cosmosPos = Transform(blade).Coordinates;
                var cosmosBlade = Spawn("HereticBladeCosmos", cosmosPos);
                EnsureComp<HereticCosmosBladeComponent>(cosmosBlade);
                Del(blade);
                comp.CurrentBlade = cosmosBlade;
                Dirty(uid, comp);
                _hands.TryPickupAnyHand(uid, cosmosBlade);
                if (TryComp<MeleeWeaponComponent>(cosmosBlade, out var cosmelee))
                {
                    cosmelee.Damage.DamageDict["Radiation"] = FixedPoint2.New(12);
                    Dirty(cosmosBlade, cosmelee);
                }
                return;
            }
        }

        Dirty(blade, melee);
    }

}
