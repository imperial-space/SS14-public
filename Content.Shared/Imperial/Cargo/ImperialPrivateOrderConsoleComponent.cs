using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>Консоль карго, у которой есть личные заказы (self_paid).</summary>
[RegisterComponent]
public sealed partial class ImperialPrivateOrderConsoleComponent : Component
{
    [ViewVariables]
    public TimeSpan NextOrder;
}
