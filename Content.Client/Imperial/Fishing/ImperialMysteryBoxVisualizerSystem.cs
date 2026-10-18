using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Fishing;

/// <summary>Открытый сундук и парящий над ним приз (mystery_box_item).</summary>
public sealed class ImperialMysteryBoxVisualizerSystem : VisualizerSystem<ImperialMysteryBoxComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialMysteryBoxComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        AppearanceSystem.TryGetData<bool>(uid, ImperialMysteryBoxVisuals.Open, out var open, args.Component);
        AppearanceSystem.TryGetData<string>(uid, ImperialMysteryBoxVisuals.Prize, out var prize, args.Component);
        AppearanceSystem.TryGetData<bool>(uid, ImperialMysteryBoxVisuals.Claimable, out var claimable, args.Component);

        if (SpriteSystem.LayerMapTryGet(sprite, "base", out var baseLayer, false))
            SpriteSystem.LayerSetRsiState(sprite, baseLayer, open ? "treasureopen" : "treasure");

        if (!SpriteSystem.LayerMapTryGet(sprite, "prize", out var prizeLayer, false))
            return;

        if (string.IsNullOrEmpty(prize))
        {
            SpriteSystem.LayerSetVisible(sprite, prizeLayer, false);
            return;
        }

        SpriteSystem.LayerSetTexture(sprite, prizeLayer, SpriteSystem.GetPrototypeIcon(prize).Default);
        SpriteSystem.LayerSetVisible(sprite, prizeLayer, true);
        SpriteSystem.LayerSetColor(sprite, prizeLayer, claimable ? Color.LightYellow : Color.White);
    }
}
