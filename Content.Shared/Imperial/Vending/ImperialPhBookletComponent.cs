using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>ph_booklet TG: из книжки вырываются полоски индикатора.</summary>
[RegisterComponent]
public sealed partial class ImperialPhBookletComponent : Component
{
    [DataField]
    public int Pages = 50;

    [DataField]
    public EntProtoId Strip = "ImperialPhPaper";
}
