using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Eldritch whetstone (sharpener/cult).</summary>
[RegisterComponent]
public sealed partial class CultWhetstoneComponent : Component
{
    [DataField] public int Uses = 1;
    [DataField("damageBonus")] public float Increment = 5f;
    [DataField] public float Max = 40f;
    [DataField] public string Prefix = "darkened";
}
