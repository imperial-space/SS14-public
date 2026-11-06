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
