using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

[Serializable, NetSerializable]
public sealed class PandemicCreateVaccineMessage : BoundUserInterfaceMessage
{
    public string Id { get; }

    public PandemicCreateVaccineMessage(string id)
    {
        Id = id;
    }
}
