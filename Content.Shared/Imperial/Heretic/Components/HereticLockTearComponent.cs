using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Heretic.Components;

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
