using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Vending;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Vending;

/// <summary>Цвет использованной полоски pH (CONVERT_PH_TO_COLOR) и состояние книжки.</summary>
public sealed class ImperialPhPaperVisualizerSystem : VisualizerSystem<ImperialPhPaperComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialPhPaperComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !AppearanceSystem.TryGetData<string>(uid, ImperialPhBookletVisuals.State, out var hex, args.Component))
            return;

        if (Color.TryFromHex(hex) is { } color)
            SpriteSystem.SetColor((uid, args.Sprite), color);
    }
}
