using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Blood beam: 9 с заряда + 9 с стрельбы.</summary>
[RegisterComponent]
public sealed partial class CultBloodBeamComponent : Component
{
    [ViewVariables] public bool Charging;
    [ViewVariables] public bool Firing;
    [ViewVariables] public Angle Angle;
    [ViewVariables] public int Pulses;
    [ViewVariables] public TimeSpan NextPulse;
    [ViewVariables] public float Spread = 40f;
    [ViewVariables] public bool Second;
    [ViewVariables] public EntityUid? Shield;
    [ViewVariables] public EntityUid? User;
    [ViewVariables] public List<EntityUid> ChargeEffects = new();

    [DataField]
    public TimeSpan PulseDelay = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan StartBeamDelay = TimeSpan.FromSeconds(9);

    [DataField]
    public TimeSpan UpdateBeamsDuration = TimeSpan.FromSeconds(3f);
}
