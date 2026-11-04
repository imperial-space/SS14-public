using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>implant/chem: 50 ед. реагентов, вводятся удалённо с консоли.</summary>
[RegisterComponent]
public sealed partial class ChemImplantComponent : Component
{
    public const string SolutionName = "implant";

    /// <summary>Раствор имплантера, который переливается в имплант при вживлении.</summary>
    [DataField]
    public string ImplanterSolution = "implanter";

    /// <summary>implant_sizes.</summary>
    [DataField]
    public List<int> Sizes = new() { 1, 5, 10 };

    [ViewVariables]
    public bool DeathTriggered;
}
