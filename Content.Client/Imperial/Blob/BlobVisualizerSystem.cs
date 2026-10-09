using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Blob;

/// <summary>Окраска блоба цветом штамма (add_atom_colour и оверлеи из SS13).</summary>
public sealed class BlobVisualizerSystem : VisualizerSystem<BlobVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, BlobVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        var color = AppearanceSystem.TryGetData<Color>(uid, BlobVisuals.Color, out var c, args.Component) ? c : Color.White;

        switch (component.Mode)
        {
            case BlobVisualsMode.Structure:
                if (AppearanceSystem.TryGetData<string>(uid, BlobVisuals.State, out var state, args.Component) &&
                    SpriteSystem.LayerMapTryGet(sprite, BlobVisualLayers.Base, out var baseLayer, false))
                    SpriteSystem.LayerSetRsiState(sprite, baseLayer, state);

                // Ядро и узел: цветной слой blob под бесцветным оверлеем.
                if (SpriteSystem.LayerMapTryGet(sprite, BlobVisualLayers.Blob, out var blobLayer, false))
                    SpriteSystem.LayerSetColor(sprite, blobLayer, color);
                else if (SpriteSystem.LayerMapTryGet(sprite, BlobVisualLayers.Base, out var tinted, false))
                    SpriteSystem.LayerSetColor(sprite, tinted, color);
                break;

            case BlobVisualsMode.Tint:
                SpriteSystem.SetColor(sprite, color);
                break;

            case BlobVisualsMode.Blobbernaut:
            {
                if (SpriteSystem.LayerMapTryGet(sprite, BlobVisualLayers.Base, out var body, false))
                    SpriteSystem.LayerSetColor(sprite, body, color);

                var alive = !AppearanceSystem.TryGetData<bool>(uid, BlobVisuals.Alive, out var a, args.Component) || a;
                if (SpriteSystem.LayerMapTryGet(sprite, BlobVisualLayers.Veins, out var veins, false))
                {
                    SpriteSystem.LayerSetVisible(sprite, veins, alive);
                    if (AppearanceSystem.TryGetData<Color>(uid, BlobVisuals.SecondaryColor, out var veinColor, args.Component))
                        SpriteSystem.LayerSetColor(sprite, veins, veinColor);
                }

                if (SpriteSystem.LayerMapTryGet(sprite, BlobVisualLayers.Eyes, out var eyes, false))
                {
                    SpriteSystem.LayerSetVisible(sprite, eyes, alive);
                    if (AppearanceSystem.TryGetData<Color>(uid, BlobVisuals.EyesColor, out var eyeColor, args.Component))
                        SpriteSystem.LayerSetColor(sprite, eyes, eyeColor);
                }

                break;
            }

            case BlobVisualsMode.Zombie:
                if (SpriteSystem.LayerMapTryGet(sprite, BlobVisualLayers.Overlay, out var head, false))
                    SpriteSystem.LayerSetColor(sprite, head, color);
                break;
        }
    }
}
