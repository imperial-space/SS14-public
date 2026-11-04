using Content.Shared.Imperial.ModularShield;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.ModularShield;

internal static class ImperialModularShieldVisualHelper
{
    public static void ApplyState(SpriteSystem sprite, SharedAppearanceSystem appearance, EntityUid uid, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !appearance.TryGetData<string>(uid, ImperialModularShieldVisuals.State, out var state, args.Component))
            return;

        if (sprite.LayerMapTryGet((uid, args.Sprite), "base", out var layer, false))
            sprite.LayerSetRsiState((uid, args.Sprite), layer, state);
    }
}
