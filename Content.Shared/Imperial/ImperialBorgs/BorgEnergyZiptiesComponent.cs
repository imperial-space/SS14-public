using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.ImperialBorgs;

[RegisterComponent]
public sealed partial class BorgEnergyZiptiesComponent : Component
{
    [DataField]
    public EntProtoId StartAction = "BorgEnergyZiptiesAction";

    [DataField]
    public EntityUid User;

    [DataField]
    public EntityUid? Action;

    [DataField]
    public EntProtoId ZiptiesPrototype = "EnergyZipties";
}
