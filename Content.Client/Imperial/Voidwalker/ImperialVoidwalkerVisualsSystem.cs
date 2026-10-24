using Content.Shared.Humanoid;
using Content.Shared.Imperial.Voidwalker;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Voidwalker;

/// <summary>Клиентская часть прохода сквозь стекло (решётки под током знает только сервер).</summary>
public sealed class ImperialGlassPasserSystem : SharedImperialGlassPasserSystem;

/// <summary>texture_limb(): части тела «опустошённого» окрашены в цвет космоса.</summary>
public sealed class ImperialVoidwalkerVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly HumanoidVisualLayers[] BodyLayers =
    {
        HumanoidVisualLayers.Chest, HumanoidVisualLayers.Head, HumanoidVisualLayers.RArm, HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand, HumanoidVisualLayers.LHand, HumanoidVisualLayers.RLeg, HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot, HumanoidVisualLayers.LFoot,
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialVoidedComponent, AfterAutoHandleStateEvent>((uid, comp, _) => Apply(uid, comp));
        SubscribeLocalEvent<ImperialVoidedComponent, ComponentStartup>((uid, comp, _) => Apply(uid, comp));
        SubscribeLocalEvent<ImperialVoidedComponent, ComponentShutdown>(OnShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Облик тела перерисовывается при смене одежды и травмах — держим окраску.
        var query = EntityQueryEnumerator<ImperialVoidedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Apply(uid, comp);
        }
    }

    private void Apply(EntityUid uid, ImperialVoidedComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        foreach (var layer in BodyLayers)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out var index, false))
                _sprite.LayerSetColor((uid, sprite), index, comp.SpaceColor);
        }
    }

    private void OnShutdown(Entity<ImperialVoidedComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        foreach (var layer in BodyLayers)
        {
            if (_sprite.LayerMapTryGet((ent, sprite), layer, out var index, false))
                _sprite.LayerSetColor((ent, sprite), index, Color.White);
        }
    }
}
