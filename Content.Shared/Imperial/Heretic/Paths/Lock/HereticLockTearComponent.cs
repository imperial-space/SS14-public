namespace Content.Shared.Imperial.Heretic.Paths.Lock;

/// <summary>
/// Разрыв в ткани реальности, созданный при возвышении пути Замка.
/// Позволяет духам войти в мир в теле монстра.
/// </summary>
[RegisterComponent]
public sealed partial class HereticLockTearComponent : Component
{
    /// <summary>Еретик, создавший разрыв.</summary>
    [DataField]
    public EntityUid Master;

}
