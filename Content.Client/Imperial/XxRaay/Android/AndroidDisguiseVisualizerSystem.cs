using Content.Shared.Humanoid;
using Content.Shared.Imperial.XxRaay.Android;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.Imperial.XxRaay.Android;

/// <summary>
/// Клиентская визуализирующая система маскировки андроида
///</summary>
public sealed partial class AndroidDisguiseVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AndroidDisguiseComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AndroidDisguiseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AndroidDisguiseComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<AndroidDisguiseComponent> ent, ref ComponentStartup args)
    {
        var uid = (EntityUid) ent;
        UpdateVisuals(uid, ent.Comp);
    }

    private void OnShutdown(Entity<AndroidDisguiseComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var uid = (EntityUid) ent;

        _sprite.LayerSetVisible((uid, sprite), ent.Comp.AndroidBaseLayer, true);
        _sprite.LayerSetVisible((uid, sprite), ent.Comp.AndroidTransformLayer, false);
        _sprite.LayerSetVisible((uid, sprite), ent.Comp.AndroidRetransformLayer, false);
        SetHumanoidVisible((uid, sprite), visible: false);
    }

    private void OnAfterState(Entity<AndroidDisguiseComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var uid = (EntityUid) ent;
        UpdateVisuals(uid, ent.Comp);
    }

    private void UpdateVisuals(EntityUid uid, AndroidDisguiseComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;


        _sprite.LayerSetVisible((uid, sprite), comp.AndroidBaseLayer,
            comp.State is AndroidDisguiseState.Android);

        _sprite.LayerSetVisible((uid, sprite), comp.AndroidTransformLayer,
            comp.State == AndroidDisguiseState.TransformingToHuman);
        _sprite.LayerSetVisible((uid, sprite), comp.AndroidRetransformLayer,
            comp.State == AndroidDisguiseState.TransformingToAndroid);

        var humanoidVisible = comp.State != AndroidDisguiseState.Android;
        SetHumanoidVisible((uid, sprite), humanoidVisible);
    }

    private void SetHumanoidVisible(Entity<SpriteComponent> ent, bool visible)
    {
        var spriteEnt = ent.AsNullable();
        
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.Chest, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.Head, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.Snout, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.Eyes, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.RArm, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.LArm, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.RLeg, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.LLeg, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.UndergarmentBottom, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.UndergarmentTop, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.LFoot, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.RFoot, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.LHand, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.RHand, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.SnoutCover, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.FacialHair, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.Hair, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.HeadSide, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.HeadTop, visible);
        _sprite.LayerSetVisible(spriteEnt, HumanoidVisualLayers.Tail, visible);
    }
}


