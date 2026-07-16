using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Lavaland.Colossus;

public sealed class ThermalEntityVisionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly ShaderInstance _unshaded;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ThermalEntityVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<SharedTransformSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _unshaded = _prototypes.Index<ShaderPrototype>(SpriteSystem.UnshadedId).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var attached = _player.LocalSession?.AttachedEntity;
        if (attached == null)
            return;

        if (!_entManager.TryGetComponent(attached, out TransformComponent? attachedXform) || attachedXform.MapID != args.MapId)
            return;

        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var query = _entManager.EntityQueryEnumerator<MobStateComponent, TransformComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out _, out var xform, out var sprite))
        {
            if (xform.MapID != args.MapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Contains(worldPos))
                continue;

            var worldRot = _transform.GetWorldRotation(xform);
            var oldShader = sprite.PostShader;

            sprite.PostShader = _unshaded;
            _sprite.RenderSprite((uid, sprite), args.WorldHandle, eyeRotation, worldRot, worldPos);
            sprite.PostShader = oldShader;
        }
    }
}