using System.Numerics;
using Content.Shared.Imperial.Uplink.AssaultPod;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Uplink;

/// <summary>Метка посадки пода: поворачивается на 90°, а внутренний ползунок сжимается к моменту касания.</summary>
public sealed class AssaultPodLandingMarkerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AssaultPodLandingMarkerComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var marker, out var sprite))
        {
            var total = (float) (marker.Arrival - marker.Start).TotalSeconds;
            var progress = total <= 0 ? 1f : Math.Clamp((float) (now - marker.Start).TotalSeconds / total, 0f, 1f);
            var ent = (uid, sprite);

            if (_sprite.LayerMapTryGet(ent, "lz", out var lz, false))
                _sprite.LayerSetRotation(ent, lz, Angle.FromDegrees(-90 * progress));

            if (_sprite.LayerMapTryGet(ent, "slider", out var slider, false))
                _sprite.LayerSetScale(ent, slider, Vector2.One * MathHelper.Lerp(1.5f, 0.01f, progress));
        }
    }
}
