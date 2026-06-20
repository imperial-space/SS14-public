using Content.Shared.Imperial.Lavaland;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Lavaland;

public sealed class AshDrakeVisualizerSystem : VisualizerSystem<AshDrakeAppearanceComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, AshDrakeAppearanceComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // When flying, hide the real drake sprite entirely
        if (AppearanceSystem.TryGetData<bool>(uid, AshDrakeVisuals.Flying, out var flying, args.Component))
        {
            SpriteSystem.SetVisible((uid, args.Sprite), !flying);
        }
    }
}
