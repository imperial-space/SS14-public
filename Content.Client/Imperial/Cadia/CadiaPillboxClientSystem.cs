using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client.Imperial.Cadia;

/// <summary>Кто стоит внутри ДОТа, видит крышу полупрозрачной: себя, орудие и пол под ногами.</summary>
public sealed class CadiaPillboxClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float RoofAlpha = 0.25f;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_player.LocalEntity is not { } local)
            return;
        var at = _xform.GetMapCoordinates(local);

        var query = EntityQueryEnumerator<CadiaPillboxComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pillbox, out var sprite, out var xform))
        {
            var center = _xform.GetMapCoordinates(uid, xform);
            var offset = at.Position - center.Position;
            var inside = at.MapId == center.MapId && Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) <= pillbox.InteriorHalf;
            var alpha = inside ? RoofAlpha : 1f;
            if (Math.Abs(sprite.Color.A - alpha) > 0.01f)
                _sprite.SetColor((uid, sprite), Color.White.WithAlpha(alpha));
        }
    }
}
