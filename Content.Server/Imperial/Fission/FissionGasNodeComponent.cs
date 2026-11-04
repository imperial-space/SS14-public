using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Газовый узел реактора: подача, откачка или замедлитель (reactor_gas_node).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionGasNodeComponent : Component
{
    public const float MinimumMoles = 3;

    [DataField] public bool Moderator;
    [DataField] public bool Intake = true;
    [DataField] public string NodeName = "pipe";
    [DataField] public float TargetPressure = 100000;
    [ViewVariables] public EntityUid? LinkedReactor;
    [DataField] public SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");
    [DataField] public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}
