using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Сепиевая «дежавю»: через 10 секунд возвращает существо и его урон.</summary>
[RegisterComponent]
public sealed partial class SlimeDejavuComponent : Component
{
    [ViewVariables]
    public EntityCoordinates Origin;

    [ViewVariables]
    public Dictionary<string, Content.Shared.FixedPoint.FixedPoint2> Damage = new();

    [ViewVariables]
    public TimeSpan RewindAt;

    [ViewVariables]
    public int Rewinds = 1;

    [DataField]
    public TimeSpan RewindDelay = TimeSpan.FromSeconds(10);
}
