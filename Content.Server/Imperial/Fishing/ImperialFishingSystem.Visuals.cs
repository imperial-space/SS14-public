using Content.Shared.Imperial.Fishing;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Fishing;

public sealed partial class ImperialFishingSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private void InitializeVisuals()
    {
        SubscribeLocalEvent<ImperialFishingRodComponent, MapInitEvent>((uid, comp, _) => UpdateRodVisuals((uid, comp)));
        SubscribeLocalEvent<ImperialFishingRodComponent, EntInsertedIntoContainerMessage>((uid, comp, _) => UpdateRodVisuals((uid, comp)));
        SubscribeLocalEvent<ImperialFishingRodComponent, EntRemovedFromContainerMessage>((uid, comp, _) => UpdateRodVisuals((uid, comp)));
        SubscribeLocalEvent<ImperialFishingRodComponent, ItemToggledEvent>(OnRodToggled);
    }

    private void OnRodToggled(Entity<ImperialFishingRodComponent> ent, ref ItemToggledEvent args)
    {
        // Сложенная удочка обрывает леску.
        if (!args.Activated)
        {
            ClearHooked(ent);
            if (args.User is { } user && _challenges.TryGetValue(user, out var challenge) && challenge.Rod == ent.Owner)
                Interrupt(challenge);
        }

        UpdateRodVisuals(ent);
    }

    /// <summary>update_overlays(): цвет лески, наложение крючка и наживки.</summary>
    private void UpdateRodVisuals(Entity<ImperialFishingRodComponent> ent)
    {
        UpdateRodUi(ent);
        var rod = GetRodInfo(ent);
        var visuals = CompOrNull<ImperialFishingRodVisualsComponent>(ent);
        var color = rod.LineComp?.LineColor ?? visuals?.DefaultLineColor ?? Color.Gray;

        _appearance.SetData(ent, ImperialFishingRodVisuals.LineColor, color);
        _appearance.SetData(ent, ImperialFishingRodVisuals.Hook, rod.Hook != null || rod.Bait != null ? rod.HookComp?.RodOverlay ?? "hook_overlay" : "");

        var bait = "";
        if (rod.Bait is { } baitEnt)
        {
            bait = CompOrNull<ImperialFishingBaitComponent>(baitEnt)?.RodOverlay
                   ?? (HasComp<Content.Shared.PowerCell.Components.PowerCellComponent>(baitEnt) ? "battery_overlay" : "worm_overlay");
        }

        _appearance.SetData(ent, ImperialFishingRodVisuals.Bait, bait);
        _appearance.SetData(ent, ImperialFishingRodVisuals.Collapsed, ent.Comp.RequiresToggle && !_toggle.IsActivated(ent.Owner));
    }
}
