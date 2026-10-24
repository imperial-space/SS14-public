using Content.Shared.Imperial.ModModules;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.ModModules;

/// <summary>
/// Покачивание спрайта носителя модуля переваливания при ходьбе.
/// </summary>
public sealed class ImperialModWaddleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialModWaddleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ImperialModWaddleComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetRotation((ent, sprite), Angle.Zero);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var time = (float) _timing.RealTime.TotalSeconds;
        var query = EntityQueryEnumerator<ImperialModWaddleComponent, SpriteComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var waddle, out var sprite, out var physics))
        {
            var moving = physics.LinearVelocity.LengthSquared() > 0.01f;
            var angle = moving ? MathF.Sin(time * waddle.Frequency * MathF.PI) * waddle.Angle : 0f;
            _sprite.SetRotation((uid, sprite), Angle.FromDegrees(angle));
        }
    }
}
