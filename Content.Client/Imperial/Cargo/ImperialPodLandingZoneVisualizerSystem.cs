using System.Numerics;
using Content.Shared.Imperial.Cargo;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cargo;

/// <summary>pod_landingzone: метка поворачивается на 90°, а внутренний ползунок сжимается к прилёту капсулы.</summary>
public sealed class ImperialPodLandingZoneVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialPodLandingZoneComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zone, out var sprite))
        {
            var total = (float)(zone.Arrival - zone.Start).TotalSeconds;
            var progress = total <= 0 ? 1f : Math.Clamp((float)(now - zone.Start).TotalSeconds / total, 0f, 1f);
            var ent = (uid, sprite);

            if (_sprite.LayerMapTryGet(ent, "lz", out var lz, false))
                _sprite.LayerSetRotation(ent, lz, Angle.FromDegrees(-90 * progress));

            if (_sprite.LayerMapTryGet(ent, "slider", out var slider, false))
                _sprite.LayerSetScale(ent, slider, Vector2.One * MathHelper.Lerp(1.5f, 0.01f, progress));
        }
    }
}
