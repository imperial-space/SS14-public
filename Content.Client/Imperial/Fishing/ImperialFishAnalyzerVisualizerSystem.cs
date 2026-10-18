using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Fishing;

/// <summary>Корпус анализатора рыб окрашен в случайный цвет (case_color).</summary>
public sealed class ImperialFishAnalyzerVisualizerSystem : VisualizerSystem<ImperialFishAnalyzerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialFishAnalyzerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !AppearanceSystem.TryGetData<Color>(uid, ImperialFishAnalyzerVisuals.CaseColor, out var color, args.Component))
            return;

        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), "case", out var layer, false))
            SpriteSystem.LayerSetColor((uid, args.Sprite), layer, color);
    }
}
