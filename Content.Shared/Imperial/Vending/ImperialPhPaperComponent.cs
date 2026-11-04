using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>ph_paper TG: окрашивается по pH раствора.</summary>
[RegisterComponent]
public sealed partial class ImperialPhPaperComponent : Component
{
    [DataField]
    public bool Used;
}
