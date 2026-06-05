using Content.Shared.Imperial.Lavaland;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Lavaland;

public sealed class BubblegumVisualizerSystem : VisualizerSystem<BubblegumAppearanceComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, BubblegumAppearanceComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<bool>(uid, BubblegumVisuals.Raging, out var raging, args.Component))
        {
            // Red tint during rage, normal colour otherwise
            args.Sprite.Color = raging
                ? new Robust.Shared.Maths.Color(1f, 0.25f, 0.25f, 1f)
                : Robust.Shared.Maths.Color.White;
        }
    }
}
