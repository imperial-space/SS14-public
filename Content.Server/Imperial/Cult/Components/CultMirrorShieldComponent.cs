using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Mirror shield.</summary>
[RegisterComponent]
public sealed partial class CultMirrorShieldComponent : Component
{
    [DataField] public int Illusions = 2;
    [ViewVariables] public List<TimeSpan> Restore = new();
}
