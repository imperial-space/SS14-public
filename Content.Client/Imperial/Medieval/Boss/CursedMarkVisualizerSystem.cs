using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class CursedMarkVisualizerSystem : EntitySystem
{
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursedMarkComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, CursedMarkComponent component, ref AfterAutoHandleStateEvent args)
    {
        component.FlameEntity = GetEntity(component.NetEntity);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CursedMarkComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.FlameEntity.IsValid())
                comp.FlameEntity = GetEntity(comp.NetEntity);

            if (!_sprite.TryGetLayer(comp.FlameEntity, 0, out var layer, true))
                return;

            var color = (_lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(Transform(uid).Coordinates, 1.5f).Count > 1) switch
            {
                true => comp.ActiveColor,
                false => comp.InactiveColor
            };

            _light.SetColor(comp.FlameEntity, color);
            layer.Color = color;
        }
    }
}
