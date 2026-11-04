using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Кровавый охладитель: перезаряжается кровью владельца.</summary>
[RegisterComponent]
public sealed partial class SlimeBloodChillerComponent : Component
{
    [ViewVariables]
    public TimeSpan NextCharge;
}
