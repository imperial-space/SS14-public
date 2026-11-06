using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>block_chance оружия культа (парирует только культист).</summary>
[RegisterComponent]
public sealed partial class CultParryComponent : Component
{
    [DataField] public float Chance = 0.25f;
    [DataField] public float WieldedMultiplier = 1f;
}
