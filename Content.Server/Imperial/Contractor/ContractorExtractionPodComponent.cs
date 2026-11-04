using Content.Shared.Imperial.Contractor;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Contractor;

/// <summary>closet/supplypod/extractionpod.</summary>
[RegisterComponent]
public sealed partial class ContractorExtractionPodComponent : Component
{
    [ViewVariables]
    public EntityUid ContractorMind;

    [ViewVariables]
    public int ContractId;

    [ViewVariables]
    public bool Used;
}
