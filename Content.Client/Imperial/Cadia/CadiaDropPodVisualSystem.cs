using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Полёт когтистой капсулы: спрайт падает с высоты на свою тень с ускорением и так же уходит вверх.
/// Тень растёт и темнеет по мере приближения капсулы к земле.
/// </summary>
public sealed class CadiaDropPodVisualSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CadiaDropPodComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var pod, out var sprite))
        {
            var t = Math.Clamp((float) ((now - pod.Start).TotalSeconds / pod.Duration), 0f, 1f);
            // Падение — с ускорением к земле, подъём — с ускорением от неё.
            var height = pod.Leaving ? pod.Height * t * t : pod.Height * (1f - t) * (1f - t);
            var near = 1f - height / pod.Height;
            var ent = (uid, sprite);
            if (_sprite.LayerMapTryGet(ent, CadiaDropPodLayers.Pod, out var podLayer, false))
            {
                _sprite.LayerSetOffset(ent, podLayer, new Vector2(0f, height));
                _sprite.LayerSetScale(ent, podLayer, new Vector2(1f + 0.25f * (1f - near)));
            }
            if (_sprite.LayerMapTryGet(ent, CadiaDropPodLayers.Shadow, out var shadowLayer, false))
            {
                _sprite.LayerSetScale(ent, shadowLayer, new Vector2(0.35f + 0.65f * near));
                _sprite.LayerSetColor(ent, shadowLayer, Color.White.WithAlpha(0.25f + 0.55f * near));
            }
        }
    }
}
