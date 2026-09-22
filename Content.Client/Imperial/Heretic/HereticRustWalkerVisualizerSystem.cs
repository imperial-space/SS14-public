using Content.Client.DamageState;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticRustWalkerVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, (bool moving, Direction dir)> _states = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRustWalkerComponent, ComponentRemove>(OnRemove);
    }

    private void OnRemove(EntityUid uid, HereticRustWalkerComponent _, ComponentRemove args)
    {
        _states.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticRustWalkerComponent, SpriteComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var physics))
        {
            var isMoving = physics.LinearVelocity.LengthSquared() > 0.01f;
            var dir = Transform(uid).LocalRotation.GetDir();

            _states.TryGetValue(uid, out var prev);
            if (prev.moving == isMoving && prev.dir == dir)
                continue;

            _states[uid] = (isMoving, dir);

            if (!_sprite.LayerMapTryGet((uid, sprite), DamageStateVisualLayers.Base, out _, false))
                continue;

            var stateName = dir == Direction.North ? "rust_walker_n" : "rust_walker_s";

            _sprite.LayerSetRsiState((uid, sprite), DamageStateVisualLayers.Base, stateName);
            _sprite.LayerSetAutoAnimated((uid, sprite), DamageStateVisualLayers.Base, isMoving);
            if (!isMoving)
                _sprite.LayerSetAnimationTime((uid, sprite), DamageStateVisualLayers.Base, 0f);
        }
    }
}
