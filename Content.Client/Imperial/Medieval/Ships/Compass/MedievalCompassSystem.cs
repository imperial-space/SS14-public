using Content.Shared.Imperial.Medieval.Ships.Compass;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Medieval.Ships.Compass;

public sealed class MedievalCompassSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var angle = new Angle(Math.PI / 2.0) + _eyeManager.CurrentEye.Rotation;
        var snappedIndex = (int) Math.Round(angle.Reduced().Theta / (Math.PI / 4));
        snappedIndex = ((snappedIndex % 8) + 8) % 8;
        var state = MedievalCompassComponent.DirectionStates[snappedIndex];

        var query = EntityQueryEnumerator<MedievalCompassComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            _sprite.LayerSetRsiState((uid, sprite), MedievalCompassLayers.Base, state);
        }
    }
}
