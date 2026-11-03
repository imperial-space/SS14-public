using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client.Imperial.Blueprints;

/// <summary>Предпросмотр чертежа на курсоре: каждая клетка — зелёная рамка с иконкой постройки.</summary>
public sealed class ImperialBlueprintOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly ImperialBlueprintSystem _blueprints;
    private readonly SharedTransformSystem _xform;
    private readonly SharedMapSystem _map;

    private static readonly Color Fill = new(48, 255, 48, 40);
    private static readonly Color Border = new(48, 255, 48, 160);
    private static readonly Color IconTint = new(160, 255, 160, 170);

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ImperialBlueprintOverlay()
    {
        IoCManager.InjectDependencies(this);
        _blueprints = _entMan.System<ImperialBlueprintSystem>();
        _xform = _entMan.System<SharedTransformSystem>();
        _map = _entMan.System<SharedMapSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _blueprints.Preview() != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_blueprints.Preview() is not { } preview
            || _xform.GetGrid(preview.Coords) is not { } gridUid
            || !_entMan.TryGetComponent<MapGridComponent>(gridUid, out var grid)
            || _xform.GetMapId(gridUid) != args.MapId)
            return;

        var handle = args.WorldHandle;
        var origin = _map.TileIndicesFor(gridUid, grid, preview.Coords);
        var size = grid.TileSize;

        // Рисуем в осях сетки: на повёрнутой станции клетки совпадут с полом.
        handle.SetTransform(_xform.GetWorldMatrix(gridUid));
        foreach (var (entry, tile, direction) in _blueprints.Layout(preview.Blueprint, origin, preview.Direction))
        {
            var box = Box2.FromDimensions(tile * size, new System.Numerics.Vector2(size, size));
            handle.DrawRect(box, Fill);
            handle.DrawRect(box, Border, false);

            if (_blueprints.Icon(entry.Construction) is { } icon)
            {
                var center = box.Center;
                var rotated = new Box2Rotated(box.Enlarged(-0.08f * size), direction.ToAngle(), center);
                handle.DrawTextureRect(icon, rotated, IconTint);
            }
        }

        handle.SetTransform(System.Numerics.Matrix3x2.Identity);
    }
}
