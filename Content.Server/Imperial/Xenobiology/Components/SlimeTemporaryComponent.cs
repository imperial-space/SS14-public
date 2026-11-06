using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Временная стена из слизи (forcefield/slimewall).</summary>
[RegisterComponent]
public sealed partial class SlimeTemporaryComponent : Component
{
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromSeconds(10f);
}
