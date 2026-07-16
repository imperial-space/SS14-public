using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.CowPortalBlade;

/// <summary>
/// Клинок Портала: при использовании в руке открывает портал, из которого появляются коровы.
/// </summary>
[RegisterComponent]
public sealed partial class CowPortalBladeComponent : Component
{
    /// <summary>Прототип портала, который будет заспавнен.</summary>
    [DataField]
    public EntProtoId PortalPrototype = "CowPortal";

    /// <summary>Кулдаун между использованиями (секунды).</summary>
    [DataField]
    public float CooldownSeconds = 30f;

    /// <summary>Время, когда можно использовать снова.</summary>
    [DataField]
    public TimeSpan NextUseTime;
}
