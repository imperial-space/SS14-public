using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.Imperial.Heretic.FleshAscension;

[RegisterComponent]
public sealed partial class HereticWormComponent : Component
{
    public List<EntityUid> Segments = new();

    // PathPoints[0] = последняя записанная точка головы, PathPoints[i+1] = позиция сегмента i.
    // Новая точка добавляется только при реальном смещении >= SegmentSpacing → при остановке сегменты замирают.
    public List<EntityCoordinates> PathPoints = new();

    // Дистанция между сегментами в пространственных единицах.
    public float SegmentSpacing = 0.5f;

    // Последний валидный угол головы — устанавливается каждый тик, чтобы перекрывать поворот от системы движения.
    public Angle LastHeadAngle = Angle.Zero;

    public EntityUid? OriginalBody;
}
