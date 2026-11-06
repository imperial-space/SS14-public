using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>machinery/monkey_recycler.</summary>
[RegisterComponent]
public sealed partial class SlimeMonkeyRecyclerComponent : Component
{
    /// <summary>stored_matter.</summary>
    [DataField]
    public float StoredMatter;

    /// <summary>cube_production: 0.1 за сервопривод и 0.1 за бункер первого уровня.</summary>
    [DataField]
    public float CubeProduction = 0.2f;

    [DataField]
    public EntProtoId Cube = "MonkeyCube";
}
