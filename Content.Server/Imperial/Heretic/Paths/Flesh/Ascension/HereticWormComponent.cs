using Robust.Shared.Map;

namespace Content.Server.Imperial.Heretic.Paths.Flesh.Ascension;

[RegisterComponent]
public sealed partial class HereticWormComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Segments = new();

    // PathPoints[0] = последняя записанная точка головы, PathPoints[i+1] = позиция сегмента i.
    // Новая точка добавляется только при реальном смещении >= SegmentSpacing → при остановке сегменты замирают.
    [ViewVariables]
    public List<EntityCoordinates> PathPoints = new();

    // Дистанция между сегментами в пространственных единицах.
    [ViewVariables]
    public float SegmentSpacing = 0.5f;

    // Последний валидный угол головы — устанавливается каждый тик, чтобы перекрывать поворот от системы движения.
    [ViewVariables]
    public Angle LastHeadAngle = Angle.Zero;

    [ViewVariables]
    public EntityUid? OriginalBody;
}
