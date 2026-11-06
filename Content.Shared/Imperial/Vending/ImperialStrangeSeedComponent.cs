using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>seeds/random TG: случайное растение со случайными реагентами.</summary>
[RegisterComponent]
public sealed partial class ImperialStrangeSeedComponent : Component
{
    [DataField]
    public List<string> Reagents = new();
}
