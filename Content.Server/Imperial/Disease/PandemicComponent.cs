using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>PanD.E.M.I.C 2200.</summary>
[RegisterComponent]
public sealed partial class PandemicComponent : Component
{
    /// <summary>Машина перезаряжается после печати.</summary>
    [ViewVariables]
    public TimeSpan? ReadyAt;

    [DataField]
    public TimeSpan CultureDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan VaccineDelay = TimeSpan.FromSeconds(20);

    [DataField]
    public string BeakerSlot = "beakerSlot";

    [DataField]
    public EntProtoId BottleProto = "ImperialDiseaseBottle";
}
