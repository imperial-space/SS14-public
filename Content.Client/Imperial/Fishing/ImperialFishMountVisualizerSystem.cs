using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Fishing;

/// <summary>Рыба на подставке (vis_contents): поверх подставки рисуется значок рыбы.</summary>
public sealed class ImperialFishMountVisualizerSystem : VisualizerSystem<ImperialFishMountComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialFishMountComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !SpriteSystem.LayerMapTryGet((uid, args.Sprite), "fish", out var layer, false))
            return;

        AppearanceSystem.TryGetData<string>(uid, ImperialFishMountVisuals.Fish, out var proto, args.Component);
        if (string.IsNullOrEmpty(proto))
        {
            SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, false);
            return;
        }

        SpriteSystem.LayerSetTexture((uid, args.Sprite), layer, SpriteSystem.GetPrototypeIcon(proto).Default);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, true);
    }
}
