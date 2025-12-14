using Content.Shared.Imperial.Medieval.SkeletonInvasion;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.SkeletonInvasion;

public sealed class SkullBossStandSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkullBossStandComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, SkullBossStandComponent component, ref AfterAutoHandleStateEvent args)
    {
        foreach (var idx in component.AttachedParts)
        {
            if (!_sprite.TryGetLayer(uid, $"{idx}", out var layer, false))
                continue;

            var state = idx.Value ? "holy-" : "" + $"skull{idx.Key}";
            _sprite.LayerSetRsiState(layer, new(state));
            _sprite.LayerSetVisible(layer, true);
        }
    }
}
