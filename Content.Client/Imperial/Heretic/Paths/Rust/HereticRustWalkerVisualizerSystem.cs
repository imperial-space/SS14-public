using Content.Client.DamageState;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.Client.Imperial.Heretic.Paths.Rust;

public sealed class HereticRustWalkerVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const float MovingVelocitySquared = 0.01f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticRustWalkerComponent, SpriteComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var walker, out var sprite, out var physics))
        {
            var isMoving = physics.LinearVelocity.LengthSquared() > MovingVelocitySquared;
            var dir = Transform(uid).LocalRotation.GetDir();

            if (walker.VisualMoving == isMoving && walker.VisualDirection == dir)
                continue;

            walker.VisualMoving = isMoving;
            walker.VisualDirection = dir;

            var spriteEnt = (uid, sprite);
            if (!_sprite.LayerMapTryGet(spriteEnt, DamageStateVisualLayers.Base, out _, false))
                continue;

            var state = dir == Direction.North ? walker.NorthState : walker.SouthState;
            _sprite.LayerSetRsiState(spriteEnt, DamageStateVisualLayers.Base, state);
            _sprite.LayerSetAutoAnimated(spriteEnt, DamageStateVisualLayers.Base, isMoving);
            if (!isMoving)
                _sprite.LayerSetAnimationTime(spriteEnt, DamageStateVisualLayers.Base, 0f);
        }
    }
}
