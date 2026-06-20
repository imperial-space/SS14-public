using Content.Shared.Examine;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Handles the percentage-based upgrade capacity system for mining PKA.
/// Works alongside the vanilla GunUpgradeSystem for stat modifications.
/// </summary>
public sealed class MiningGunUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MiningUpgradeableGunComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MiningUpgradeableGunComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<MiningUpgradeableGunComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<MiningUpgradeableGunComponent, GunRefreshModifiersEvent>(RelayEvent);
        SubscribeLocalEvent<MiningUpgradeableGunComponent, GunShotEvent>(RelayEvent);
    }

    private void RelayEvent<T>(Entity<MiningUpgradeableGunComponent> ent, ref T args) where T : notnull
    {
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            RaiseLocalEvent(upgrade, ref args);
        }
    }

    private void OnInit(Entity<MiningUpgradeableGunComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.UpgradesContainerId);
    }

    private void OnAfterInteractUsing(Entity<MiningUpgradeableGunComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!TryComp<MiningGunUpgradeComponent>(args.Used, out var miningUpgrade))
            return;

        if (!TryComp<GunUpgradeComponent>(args.Used, out _))
            return;

        if (_whitelist.IsWhitelistFail(ent.Comp.Whitelist, args.Used))
            return;

        var usedCapacity = GetUsedCapacity(ent);
        if (usedCapacity + miningUpgrade.SlotCost > ent.Comp.MaxCapacity)
        {
            _popup.PopupPredicted(Loc.GetString("mining-upgradeable-gun-popup-capacity-full"), ent, args.User);
            return;
        }

        _audio.PlayPredicted(ent.Comp.InsertSound, ent, args.User);
        _popup.PopupClient(Loc.GetString("gun-upgrade-popup-insert", ("upgrade", args.Used), ("gun", ent.Owner)), args.User);
        _gun.RefreshModifiers(ent.Owner);
        args.Handled = _container.Insert(args.Used, _container.GetContainer(ent, ent.Comp.UpgradesContainerId));

        RefreshRechargeCooldown(ent);
    }

    private void OnExamine(Entity<MiningUpgradeableGunComponent> ent, ref ExaminedEvent args)
    {
        var used = GetUsedCapacity(ent);
        args.PushMarkup(Loc.GetString(ent.Comp.ExamineCapacityText, ("used", used), ("max", ent.Comp.MaxCapacity)));

        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            if (TryComp<GunUpgradeComponent>(upgrade, out var comp))
                args.PushMarkup(Loc.GetString(comp.ExamineText));
        }
    }

    /// <summary>
    /// Returns all upgrade entities currently in this gun's upgrade container.
    /// </summary>
    public IEnumerable<EntityUid> GetCurrentUpgrades(Entity<MiningUpgradeableGunComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.UpgradesContainerId, out var container))
            yield break;

        foreach (var uid in container.ContainedEntities)
            yield return uid;
    }

    /// <summary>
    /// Calculates total slot cost of all installed upgrades.
    /// </summary>
    public int GetUsedCapacity(Entity<MiningUpgradeableGunComponent> ent)
    {
        var total = 0;
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            if (TryComp<MiningGunUpgradeComponent>(upgrade, out var comp))
                total += comp.SlotCost;
        }
        return total;
    }

    /// <summary>
    /// Recalculates and applies the recharge cooldown based on installed recharge-reduction upgrades.
    /// </summary>
    private void RefreshRechargeCooldown(Entity<MiningUpgradeableGunComponent> ent)
    {
        if (!TryComp<RechargeBasicEntityAmmoComponent>(ent, out var recharge))
            return;

        var totalReduction = 0f;
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            if (TryComp<GunUpgradeRechargeReductionComponent>(upgrade, out var red))
                totalReduction += red.ReductionSeconds;
        }

        recharge.RechargeCooldown = Math.Max(0.5f, ent.Comp.BaseRechargeCooldown - totalReduction);
        Dirty(ent.Owner, recharge);
    }
}
