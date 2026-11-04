using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Личинка: растёт (amount_grown) и может эволюционировать, когда вырастет.</summary>
[RegisterComponent]
public sealed partial class XenoLarvaComponent : Component
{
    [DataField]
    public float Growth;

    [DataField]
    public float MaxGrowth = 100;

    [DataField]
    public List<EntProtoId> Castes = new() { "ImperialXenoHunter", "ImperialXenoSentinel", "ImperialXenoDrone" };

    [ViewVariables]
    public bool Hidden;
}
