namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

/// <summary>
/// Состояние сущности относительно космических полей еретика.
/// </summary>
[RegisterComponent]
public sealed partial class HereticCosmicFieldAffectedComponent : Component
{
    /// <summary>Снаряд уже замедлен полем — повторно не замедляем.</summary>
    [ViewVariables]
    public bool ProjectileSlowed;

    /// <summary>До этого момента поле не отталкивает существо повторно.</summary>
    [ViewVariables]
    public TimeSpan PushImmuneUntil;
}
