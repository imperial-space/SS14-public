using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Fishing;

/// <summary>update_overlays() генератора порталов: включённый портал и картинка измерения.</summary>
public sealed class ImperialFishingPortalVisualizerSystem : VisualizerSystem<ImperialFishingPortalComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialFishingPortalComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        AppearanceSystem.TryGetData<string>(uid, ImperialFishingPortalVisuals.Overlay, out var overlay, args.Component);
        var active = !string.IsNullOrEmpty(overlay);

        foreach (var key in new[] { "on", "emissive" })
        {
            if (SpriteSystem.LayerMapTryGet(sprite, key, out var layer, false))
                SpriteSystem.LayerSetVisible(sprite, layer, active);
        }

        if (SpriteSystem.LayerMapTryGet(sprite, "dimension", out var dimension, false))
        {
            SpriteSystem.LayerSetVisible(sprite, dimension, active);
            if (active)
                SpriteSystem.LayerSetRsiState(sprite, dimension, overlay!);
        }
    }
}
