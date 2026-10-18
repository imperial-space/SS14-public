using Content.Shared.Imperial.ModularShield;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.ModularShield;

/// <summary>update_icon_state() генератора и модулей: сервер присылает готовое имя состояния спрайта.</summary>
public sealed class ImperialModularShieldVisualizerSystem : VisualizerSystem<ImperialModularShieldGeneratorComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialModularShieldGeneratorComponent component, ref AppearanceChangeEvent args)
    {
        ImperialModularShieldVisualHelper.ApplyState(SpriteSystem, AppearanceSystem, uid, ref args);
    }
}

public sealed class ImperialModularShieldModuleVisualizerSystem : VisualizerSystem<ImperialModularShieldModuleComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialModularShieldModuleComponent component, ref AppearanceChangeEvent args)
    {
        ImperialModularShieldVisualHelper.ApplyState(SpriteSystem, AppearanceSystem, uid, ref args);
    }
}

/// <summary>Клетка щита: голубая, прозрачность по прочности генератора.</summary>
public sealed class ImperialModularShieldFieldVisualizerSystem : VisualizerSystem<ImperialModularShieldComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialModularShieldComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !AppearanceSystem.TryGetData<float>(uid, ImperialModularShieldVisuals.Alpha, out var alpha, args.Component))
            return;

        SpriteSystem.SetColor((uid, args.Sprite), args.Sprite.Color.WithAlpha(alpha));
    }
}

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
