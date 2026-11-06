using Content.Shared.Imperial.ModularShield;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.ModularShield;

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
