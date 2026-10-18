using Content.Shared.Imperial.Lavaland;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Lavaland;

public sealed class MegafaunaVisualizerSystem : VisualizerSystem<MegafaunaAppearanceComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, MegafaunaAppearanceComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var state = AppearanceSystem.TryGetData<string>(uid, MegafaunaVisuals.State, out var s, args.Component) && !string.IsNullOrEmpty(s)
            ? s
            : component.BaseState;
        SpriteSystem.LayerSetRsiState((uid, args.Sprite), 0, state);

        var color = AppearanceSystem.TryGetData<Color>(uid, MegafaunaVisuals.Color, out var c, args.Component) ? c : Color.White;
        SpriteSystem.SetColor((uid, args.Sprite), color);

        if (AppearanceSystem.TryGetData<string>(uid, MegafaunaVisuals.Overlay, out var overlay, args.Component) &&
            SpriteSystem.LayerExists((uid, args.Sprite), 1))
        {
            SpriteSystem.LayerSetVisible((uid, args.Sprite), 1, !string.IsNullOrEmpty(overlay));
            if (!string.IsNullOrEmpty(overlay))
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), 1, overlay);
        }

        if (AppearanceSystem.TryGetData<Color>(uid, MegafaunaVisuals.OverlayColor, out var overlayColor, args.Component) &&
            SpriteSystem.LayerExists((uid, args.Sprite), 1))
        {
            SpriteSystem.LayerSetColor((uid, args.Sprite), 1, overlayColor);
        }
    }
}
