using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>vending_refill/custom: хранит товар автомата при разборке и перевозке.</summary>
[RegisterComponent]
public sealed partial class ImperialCustomVendorRefillComponent : Component
{
    [DataField]
    public string Container = "storagebase";
}
