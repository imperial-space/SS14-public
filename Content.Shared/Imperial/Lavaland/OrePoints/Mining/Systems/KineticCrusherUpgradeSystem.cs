using Content.Shared.Examine;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// Manages upgrade slots for the KineticCrusher.
/// Handles insertion, examine text, UseDelay refresh (Legion Skull)
/// and ammo-prototype swapping (Watcher Wing).
/// </summary>
public sealed class KineticCrusherUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KineticCrusherUpgradeableComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<KineticCrusherUpgradeableComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<KineticCrusherUpgradeableComponent, ExaminedEvent>(OnExamine);
    }

    // ─── Init ────────────────────────────────────────────────────────────

    private void OnInit(Entity<KineticCrusherUpgradeableComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.UpgradesContainerId);
    }

    // ─── Insert ──────────────────────────────────────────────────────────

    private void OnAfterInteractUsing(Entity<KineticCrusherUpgradeableComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!TryComp<CrusherUpgradeComponent>(args.Used, out var upgrade))
            return;

        var usedCapacity = GetUsedCapacity(ent);
        if (usedCapacity + upgrade.SlotCost > ent.Comp.MaxCapacity)
        {
            _popup.PopupPredicted(
                Loc.GetString("crusher-upgrade-capacity-full"),
                ent, args.User);
            return;
        }

        var container = _container.GetContainer(ent, ent.Comp.UpgradesContainerId);
        if (!_container.Insert(args.Used, container))
            return;

        _audio.PlayPredicted(ent.Comp.InsertSound, ent, args.User);
        _popup.PopupClient(
            Loc.GetString("crusher-upgrade-inserted", ("upgrade", args.Used), ("crusher", ent.Owner)),
            args.User);

        RefreshUpgrades(ent);
        args.Handled = true;
    }

    // ─── Examine ─────────────────────────────────────────────────────────

    private void OnExamine(Entity<KineticCrusherUpgradeableComponent> ent, ref ExaminedEvent args)
    {
        var used = GetUsedCapacity(ent);
        args.PushMarkup(Loc.GetString(
            "crusher-upgrade-examine-capacity",
            ("used", used),
            ("max", ent.Comp.MaxCapacity)));

        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            if (!TryComp<CrusherUpgradeComponent>(upgrade, out var comp))
                continue;
            if (comp.ExamineText != "")
                args.PushMarkup(Loc.GetString(comp.ExamineText));
        }
    }

    // ─── Refresh ─────────────────────────────────────────────────────────

    /// <summary>
    /// Re-evaluates all passive modifiers after any change to the upgrade container.
    /// </summary>
    public void RefreshUpgrades(Entity<KineticCrusherUpgradeableComponent> ent)
    {
        RefreshUseDelay(ent);
        RefreshAmmoProto(ent);
    }

    /// <summary>
    /// Adjusts UseDelay based on installed Legion Skull upgrades.
    /// </summary>
    private void RefreshUseDelay(Entity<KineticCrusherUpgradeableComponent> ent)
    {
        if (!HasComp<UseDelayComponent>(ent))
            return;

        var totalReduction = 0f;
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            if (TryComp<CrusherLegionSkullUpgradeComponent>(upgrade, out var skull))
                totalReduction += skull.RechargeReduction;
        }

        var newDelay = TimeSpan.FromSeconds(Math.Max(0.1, ent.Comp.BaseUseDelay - totalReduction));
        _useDelay.SetLength(ent.Owner, newDelay);
    }

    /// <summary>
    /// Selects the correct ammo prototype based on installed shot-modifier upgrades.
    /// Priority (highest wins): Ashen Skull → Watcher Wing → base.
    /// Ashen Skull subsumes Watcher Wing because BulletChargeAshen already carries its own
    /// slowdown effect, so installing both chips wastes no slot capacity.
    /// </summary>
    private void RefreshAmmoProto(Entity<KineticCrusherUpgradeableComponent> ent)
    {
        if (!TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo))
            return;

        EntProtoId targetProto = ent.Comp.BaseAmmoProto;

        // First pass: look for AshenSkull (highest priority)
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            if (TryComp<CrusherAshenSkullUpgradeComponent>(upgrade, out var ashen))
            {
                targetProto = ashen.UpgradedProjectile;
                ammo.Proto = targetProto;
                Dirty(ent.Owner, ammo);
                return;
            }
        }

        // Second pass: fall back to WatcherWing if no AshenSkull
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            if (TryComp<CrusherWatcherWingUpgradeComponent>(upgrade, out var wing))
            {
                targetProto = wing.UpgradedProjectile;
                break;
            }
        }

        ammo.Proto = targetProto;
        Dirty(ent.Owner, ammo);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    /// <summary>Enumerates all upgrade entities currently in the crusher's upgrade container.</summary>
    public IEnumerable<EntityUid> GetCurrentUpgrades(Entity<KineticCrusherUpgradeableComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.UpgradesContainerId, out var container))
            yield break;

        foreach (var uid in container.ContainedEntities)
            yield return uid;
    }

    /// <summary>Returns the total slot capacity currently consumed by installed upgrades.</summary>
    public int GetUsedCapacity(Entity<KineticCrusherUpgradeableComponent> ent)
    {
        var total = 0;
        foreach (var upgrade in GetCurrentUpgrades(ent))
        {
            if (TryComp<CrusherUpgradeComponent>(upgrade, out var comp))
                total += comp.SlotCost;
        }
        return total;
    }
}
