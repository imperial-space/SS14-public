using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Призрак-культист, поддерживаемый руной Царства духов.</summary>
[RegisterComponent]
public sealed partial class CultSpiritGhostComponent : Component
{
    [ViewVariables] public EntityUid? Rune;
    [ViewVariables] public EntityUid? Invoker;
    [ViewVariables] public EntityUid? Shield;
    [ViewVariables] public TimeSpan NextDrain;
    [ViewVariables] public EntityUid? OriginalMind;
    [ViewVariables] public bool Dissolving;

    [DataField]
    public TimeSpan DrainDelay = TimeSpan.FromSeconds(0.1);

    [DataField]
    public float UpdateAmount = 0.1f;
}
