using Content.Shared.Imperial.TerrorSpider;
using Content.Shared.Mobs;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.TerrorSpider;

/// <summary>icon_state по стадии (серый на паутине, окрас принцессы, вид кокона) и смерти.</summary>
public sealed class TerrorSpiderVisualizerSystem : VisualizerSystem<TerrorSpiderVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, TerrorSpiderVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || component.Alive.Count == 0)
            return;

        var stage = AppearanceSystem.TryGetData<int>(uid, TerrorSpiderVisuals.Stage, out var s, args.Component) ? s : 0;
        stage = Math.Clamp(stage, 0, component.Alive.Count - 1);

        var dead = AppearanceSystem.TryGetData<MobState>(uid, MobStateVisuals.State, out var state, args.Component)
                   && state == MobState.Dead;

        var name = dead && component.Dead.Count > 0
            ? component.Dead[Math.Clamp(stage, 0, component.Dead.Count - 1)]
            : component.Alive[stage];

        var sprite = (uid, args.Sprite);
        if (SpriteSystem.LayerMapTryGet(sprite, TerrorSpiderVisuals.Stage, out var layer, false))
            SpriteSystem.LayerSetRsiState(sprite, layer, name);
    }
}
