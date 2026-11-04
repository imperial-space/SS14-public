using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>chair/e_chair + component/electrified_buckle.</summary>
[RegisterComponent]
public sealed partial class ElectricChairComponent : Component
{
    public const string KitContainer = "shock-kit";

    /// <summary>Из какого стула собран — в него он и превращается обратно.</summary>
    [DataField]
    public string? OriginalChair;

    /// <summary>shock_on_loop.</summary>
    [DataField]
    public bool Active;

    /// <summary>shock_loop_time.</summary>
    [DataField]
    public TimeSpan LoopTime = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextShock;

    [ViewVariables]
    public TimeSpan? ShockVisualUntil;
}
