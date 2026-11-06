using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Тело культиста, ушедшего тёмным духом.</summary>
[RegisterComponent]
public sealed partial class CultSpiritBodyComponent : Component
{
    [ViewVariables] public EntityUid? Ghost;
    [ViewVariables] public EntityUid? Rune;

    [DataField]
    public TimeSpan CheckDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan UpdateDarkSpiritsSeconds = TimeSpan.FromSeconds(0.2f);

    [DataField]
    public float UpdateDarkSpiritsSeconds2 = 4f;
}
