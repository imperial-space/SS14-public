using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>Спуск ящика с депо: ящик и купол опускаются с неба и покачиваются, тень на земле растёт. Сервер только считает время.</summary>
public sealed class CadiaSupplyDropClientSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string CrateLayer = "crate";
    private const string CanopyLayer = "canopy";
    private const string ShadowLayer = "shadow";
    private const float StartHeight = 7f;
    private const float CanopyAbove = 0.85f;
    private const float SwayWidth = 0.22f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaSupplyDropComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CadiaSupplyDropComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CadiaSupplyDropComponent> ent, ref ComponentStartup args) => ApplyIcon(ent);

    private void OnState(Entity<CadiaSupplyDropComponent> ent, ref AfterAutoHandleStateEvent args) => ApplyIcon(ent);

    /// <summary>Внизу висит именно то, что заказали: значок берётся из прототипа груза.</summary>
    private void ApplyIcon(Entity<CadiaSupplyDropComponent> ent)
    {
        if (string.IsNullOrEmpty(ent.Comp.Item) || !TryComp<SpriteComponent>(ent, out var sprite) ||
            !_sprite.LayerMapTryGet((ent, sprite), CrateLayer, out var index, false))
            return;
        _sprite.LayerSetTexture((ent, sprite), index, _sprite.GetPrototypeIcon(ent.Comp.Item).Default);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CadiaSupplyDropComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var drop, out var sprite))
        {
            var total = (float) (drop.LandAt - drop.Start).TotalSeconds;
            var t = total > 0f ? Math.Clamp((float) (now - drop.Start).TotalSeconds / total, 0f, 1f) : 1f;
            var height = (1f - t) * StartHeight;
            var sway = MathF.Sin((float) now.TotalSeconds * 2.4f) * SwayWidth * (1f - t);

            var ent = new Entity<SpriteComponent?>(uid, sprite);
            _sprite.LayerSetOffset(ent, CrateLayer, new Vector2(sway, height));
            _sprite.LayerSetOffset(ent, CanopyLayer, new Vector2(sway, height + CanopyAbove));
            _sprite.LayerSetScale(ent, ShadowLayer, new Vector2(0.35f + 0.65f * t));
            _sprite.LayerSetColor(ent, ShadowLayer, Color.White.WithAlpha(0.25f + 0.6f * t));
        }
    }
}
