using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Выходной силовой терминал реактора (reactor_power).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionPowerTerminalComponent : Component
{
    [ViewVariables] public EntityUid? Reactor;
}
