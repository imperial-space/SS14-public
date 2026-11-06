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

    [DataField]
    public TimeSpan PodEnteredDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan ExperienceDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan ReturnDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan PodEnteredDelay2 = TimeSpan.FromSeconds(3);
}
