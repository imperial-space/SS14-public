using Content.Shared.Atmos;
using Content.Shared.Imperial.Fission;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>Центрифуга обогащения топлива (nuclear_centrifuge).</summary>
[RegisterComponent, Access(typeof(FissionSystem))]
public sealed partial class FissionCentrifugeComponent : Component
{
    public const string ContainerId = "fission_centrifuge";

    [DataField] public TimeSpan WorkTime = TimeSpan.FromSeconds(10);
    [ViewVariables] public bool Active;
    [ViewVariables] public TimeSpan EndTime;
    [ViewVariables] public EntProtoId? Result;
    [ViewVariables] public EntityUid? PendingRod;
    [ViewVariables] public TimeSpan? UnpoweredSince;
    [DataField] public float IdleLoad = 200;
    [DataField] public float ActiveLoad = 3000;
    [DataField] public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Imperial/Fission/centrifuge_start.ogg");
    [DataField] public SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");
    [DataField] public SoundSpecifier BuzzSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
    [DataField] public SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
}
