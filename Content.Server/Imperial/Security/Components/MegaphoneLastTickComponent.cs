using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// Данные <see cref="MegaphoneSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class MegaphoneLastTickComponent : Component
{
    [ViewVariables]
    public GameTick Value;
}
