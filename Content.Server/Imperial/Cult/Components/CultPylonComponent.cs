using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Пилон: лечение r5 и превращение пола.</summary>
[RegisterComponent]
public sealed partial class CultPylonComponent : Component
{
    [DataField] public float Range = 5f;
    [DataField] public float BruteHeal = 0.4f;
    [DataField] public float BurnHeal = 0.4f;
    [DataField] public float BloodHeal = 0.4f;
    [DataField] public float SimpleHeal = 1.2f;
    [DataField] public float WoundClotting = 0.1f;
    [DataField] public TimeSpan CorruptionCooldown = TimeSpan.FromSeconds(5);

    [ViewVariables] public TimeSpan NextHeal;
    [ViewVariables] public TimeSpan NextCorruption;
}
