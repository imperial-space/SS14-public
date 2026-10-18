using System.Numerics;
using Content.Shared.Imperial.Changeling;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Changeling;

/// <summary>
/// Последний шанс: тело за секунду раздувается в полтора раза и наливается красным.
/// </summary>
public sealed class ImperialChangelingSwellSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialChangelingSwellComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ImperialChangelingSwellComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetScale((ent.Owner, sprite), Vector2.One);
        _sprite.SetColor((ent.Owner, sprite), Color.White);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<ImperialChangelingSwellComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var swell, out var sprite))
        {
            var progress = (float) Math.Clamp((_timing.CurTime - swell.StartTime) / swell.Duration, 0, 1);
            var scale = 1 + (swell.Scale - 1) * progress;
            _sprite.SetScale((uid, sprite), new Vector2(scale, scale));
            _sprite.SetColor((uid, sprite), Color.InterpolateBetween(Color.White, Color.Red, progress));
        }
    }
}
