using System.Numerics;
using Content.Shared.Imperial.Cargo;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cargo;

/// <summary>update_overlays() маяка: оранжевый — связан, жёлтый — готов, зелёный — капсула летит.</summary>
public sealed class ImperialSupplyPodBeaconVisualizerSystem : VisualizerSystem<ImperialSupplyPodBeaconComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialSupplyPodBeaconComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !SpriteSystem.LayerMapTryGet((uid, args.Sprite), "light", out var layer, false))
            return;

        AppearanceSystem.TryGetData<string>(uid, ImperialSupplyPodBeaconVisuals.Light, out var light, args.Component);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, !string.IsNullOrEmpty(light));
        if (!string.IsNullOrEmpty(light))
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), layer, light);
    }
}
