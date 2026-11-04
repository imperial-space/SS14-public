using Content.Shared.Imperial.ModularShield;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.ModularShield;

public sealed class ImperialModularShieldModuleVisualizerSystem : VisualizerSystem<ImperialModularShieldModuleComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialModularShieldModuleComponent component, ref AppearanceChangeEvent args)
    {
        ImperialModularShieldVisualHelper.ApplyState(SpriteSystem, AppearanceSystem, uid, ref args);
    }
}
