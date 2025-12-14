using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Inventory;

namespace Content.Server.Imperial.Medieval.Plague;

[DataDefinition]
public sealed partial class SetBloodlettingProbabilitiesEvent : EntityEventArgs
{
    [DataField(required: true)]
    public Dictionary<BloodlettingResult, Dictionary<BloodlettingResult, float>> Data;
}
