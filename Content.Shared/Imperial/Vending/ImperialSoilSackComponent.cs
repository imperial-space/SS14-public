using Content.Shared.Cargo.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>soil_sack TG: высыпать грядку на пол (тратится целиком).</summary>
[RegisterComponent]
public sealed partial class ImperialSoilSackComponent : Component
{
    [DataField]
    public EntProtoId Soil = "hydroponicsSoil";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField]
    public Robust.Shared.Audio.SoundSpecifier? Sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    /// <summary>Клетка, выбранная при начале высыпания.</summary>
    [ViewVariables]
    public Robust.Shared.Map.NetCoordinates? Target;
}
