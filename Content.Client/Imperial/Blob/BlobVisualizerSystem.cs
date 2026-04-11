using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client.Imperial.Blob;

public sealed class BlobVisualizerSystem : VisualizerSystem<BlobVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, BlobVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<Color>(uid, BlobVisuals.Color, out var color, args.Component))
            color = Color.White;

        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), BlobVisualLayers.Base, out var baseLayer, false))
            SpriteSystem.LayerSetColor((uid, args.Sprite), baseLayer, color.WithAlpha(args.Sprite[baseLayer].Color.A));

        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), BlobVisualLayers.Glow, out var glowLayer, false))
            SpriteSystem.LayerSetColor((uid, args.Sprite), glowLayer, color.WithAlpha(args.Sprite[glowLayer].Color.A));

        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), BlobVisualLayers.Overlay, out var overlayLayer, false))
            SpriteSystem.LayerSetColor((uid, args.Sprite), overlayLayer, Color.White.WithAlpha(args.Sprite[overlayLayer].Color.A));
    }
}