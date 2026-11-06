using Content.Shared.Hands;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// Наложения удочки (update_overlays / worn_overlays): катушка цвета лески, леска, крючок и наживка,
/// разложенная и сложенная телескопическая удочка.
/// </summary>
public sealed class ImperialFishingRodVisualizerSystem : VisualizerSystem<ImperialFishingRodVisualsComponent>
{
    private static readonly ResPath InhandRsi = new("/Textures/Imperial/Fishing/rod_inhands.rsi");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialFishingRodVisualsComponent, GetInhandVisualsEvent>(OnGetInhandVisuals, after: new[] { typeof(Content.Client.Items.Systems.ItemSystem) });
    }

    protected override void OnAppearanceChange(EntityUid uid, ImperialFishingRodVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        AppearanceSystem.TryGetData<Color>(uid, ImperialFishingRodVisuals.LineColor, out var color, args.Component);
        AppearanceSystem.TryGetData<string>(uid, ImperialFishingRodVisuals.Hook, out var hook, args.Component);
        AppearanceSystem.TryGetData<string>(uid, ImperialFishingRodVisuals.Bait, out var bait, args.Component);
        AppearanceSystem.TryGetData<bool>(uid, ImperialFishingRodVisuals.Collapsed, out var collapsed, args.Component);

        if (component.CollapsedState != null && component.ExtendedState != null && SpriteSystem.LayerMapTryGet(sprite, "base", out var baseLayer, false))
            SpriteSystem.LayerSetRsiState(sprite, baseLayer, collapsed ? component.CollapsedState : component.ExtendedState);

        if (SpriteSystem.LayerMapTryGet(sprite, "reel", out var reel, false))
        {
            SpriteSystem.LayerSetVisible(sprite, reel, !collapsed);
            SpriteSystem.LayerSetColor(sprite, reel, color);
        }

        var hasHook = !collapsed && !string.IsNullOrEmpty(hook);
        if (SpriteSystem.LayerMapTryGet(sprite, "line", out var line, false))
        {
            SpriteSystem.LayerSetVisible(sprite, line, hasHook);
            SpriteSystem.LayerSetColor(sprite, line, color);
        }

        if (SpriteSystem.LayerMapTryGet(sprite, "hook", out var hookLayer, false))
        {
            SpriteSystem.LayerSetVisible(sprite, hookLayer, hasHook);
            if (hasHook)
                SpriteSystem.LayerSetRsiState(sprite, hookLayer, hook!);
        }

        if (SpriteSystem.LayerMapTryGet(sprite, "bait", out var baitLayer, false))
        {
            var hasBait = !collapsed && !string.IsNullOrEmpty(bait);
            SpriteSystem.LayerSetVisible(sprite, baitLayer, hasBait);
            if (hasBait)
                SpriteSystem.LayerSetRsiState(sprite, baitLayer, bait!);
        }
    }

    /// <summary>get_fishing_worn_overlays(): катушка, леска и крючок в руке.</summary>
    private void OnGetInhandVisuals(Entity<ImperialFishingRodVisualsComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        AppearanceSystem.TryGetData<bool>(ent, ImperialFishingRodVisuals.Collapsed, out var collapsed, appearance);
        if (collapsed)
        {
            args.Layers.Clear();
            return;
        }

        AppearanceSystem.TryGetData<Color>(ent, ImperialFishingRodVisuals.LineColor, out var color, appearance);
        var side = args.Location == Content.Shared.Hands.Components.HandLocation.Left ? "left" : "right";

        args.Layers.Add(($"imperial-rod-reel-{side}", new PrototypeLayerData
        {
            RsiPath = InhandRsi.ToString(),
            State = $"reel-inhand-{side}",
            Color = color,
        }));

        // Пока ничего не зацеплено, видна свисающая леска с крючком.
        if (CompOrNull<ImperialFishingRodComponent>(ent)?.Hooked != null)
            return;

        args.Layers.Add(($"imperial-rod-line-{side}", new PrototypeLayerData
        {
            RsiPath = InhandRsi.ToString(),
            State = $"line-inhand-{side}",
            Color = color,
        }));
        args.Layers.Add(($"imperial-rod-hook-{side}", new PrototypeLayerData
        {
            RsiPath = InhandRsi.ToString(),
            State = $"hook-inhand-{side}",
        }));
    }
}
