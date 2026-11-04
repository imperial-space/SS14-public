using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/obj/machinery/computer/quantum_console.</summary>
[RegisterComponent]
public sealed partial class QuantumConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Server;
}
