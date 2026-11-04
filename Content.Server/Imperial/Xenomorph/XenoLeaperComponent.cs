using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Охотник: прыжок (pounce).</summary>
[RegisterComponent]
public sealed partial class XenoLeaperComponent : Component
{
    [DataField]
    public float MaxDistance = 7;

    [DataField]
    public float Speed = 15;

    [DataField]
    public TimeSpan TargetParalyze = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan SelfParalyze = TimeSpan.FromSeconds(4);

    [ViewVariables]
    public bool Leaping;
}
