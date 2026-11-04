using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Консоль наблюдения NGCR (fission_monitor).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionMonitorComponent : Component
{
    [ViewVariables] public EntityUid? Reactor;
    [ViewVariables] public bool Controller = true;
    [DataField] public float ControlRange = 12;
    [ViewVariables] public TimeSpan NextUpdate;
    [DataField] public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}
