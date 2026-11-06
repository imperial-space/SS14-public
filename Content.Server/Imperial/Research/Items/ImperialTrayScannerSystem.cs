using Content.Shared.Imperial.Research.Items;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.SubFloor;

namespace Content.Server.Imperial.Research.Items;

/// <summary>
/// Т-лучевые сканеры Imperial: очки включаются только на лице, а сканер, появившийся на уже надетой вещи
/// (модуль МОДа на шлеме), сразу открывает носителю подпольный слой — ванильно это делается только при надевании.
/// </summary>
public sealed class ImperialTrayScannerSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialWornTrayScannerComponent, MapInitEvent>(OnWornMapInit);
        SubscribeLocalEvent<ImperialWornTrayScannerComponent, GotEquippedEvent>(OnWornEquipped);
        SubscribeLocalEvent<ImperialWornTrayScannerComponent, GotUnequippedEvent>(OnWornUnequipped);
        SubscribeLocalEvent<ImperialWornTrayScannerComponent, ActivateInWorldEvent>(OnWornActivate, before: new[] { typeof(SharedTrayScannerSystem) });

        SubscribeLocalEvent<TrayScannerComponent, ComponentStartup>(OnScannerStartup);
        SubscribeLocalEvent<TrayScannerComponent, ComponentShutdown>(OnScannerShutdown);
    }

    private void OnWornMapInit(Entity<ImperialWornTrayScannerComponent> ent, ref MapInitEvent args)
    {
        var scanner = EnsureComp<TrayScannerComponent>(ent);
        scanner.Range = ent.Comp.Range;
        scanner.Enabled = false;
        Dirty(ent, scanner);
    }

    private void OnWornEquipped(Entity<ImperialWornTrayScannerComponent> ent, ref GotEquippedEvent args)
    {
        SetEnabled(ent, true);
    }

    private void OnWornUnequipped(Entity<ImperialWornTrayScannerComponent> ent, ref GotUnequippedEvent args)
    {
        SetEnabled(ent, false);
    }

    /// <summary>Очки не включаются кликом — только надеванием.</summary>
    private void OnWornActivate(Entity<ImperialWornTrayScannerComponent> ent, ref ActivateInWorldEvent args)
    {
        args.Handled = true;
    }

    private void SetEnabled(EntityUid uid, bool enabled)
    {
        if (!TryComp<TrayScannerComponent>(uid, out var scanner) || scanner.Enabled == enabled)
            return;

        scanner.Enabled = enabled;
        Dirty(uid, scanner);
    }

    private void OnScannerStartup(Entity<TrayScannerComponent> ent, ref ComponentStartup args)
    {
        if (WornBy(ent) is not { } user)
            return;

        var comp = EnsureComp<TrayScannerUserComponent>(user);
        if (++comp.Count == 1)
            _eye.RefreshVisibilityMask(user);
    }

    private void OnScannerShutdown(Entity<TrayScannerComponent> ent, ref ComponentShutdown args)
    {
        if (WornBy(ent) is not { } user || !TryComp<TrayScannerUserComponent>(user, out var comp))
            return;

        if (--comp.Count > 0)
            return;

        RemComp<TrayScannerUserComponent>(user);
        _eye.RefreshVisibilityMask(user);
    }

    /// <summary>Носитель, если вещь уже надета (не при надевании — тогда счётчик ведёт ванильная система).</summary>
    private EntityUid? WornBy(EntityUid item)
    {
        if (LifeStage(item) < EntityLifeStage.MapInitialized || !_inventory.TryGetContainingSlot(item, out _))
            return null;

        return Transform(item).ParentUid;
    }
}
