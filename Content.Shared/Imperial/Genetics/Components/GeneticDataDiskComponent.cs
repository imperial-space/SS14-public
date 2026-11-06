using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Components;

/// <summary>Генетическая дискета (disk/data из SS13): мутации и один генетический облик.</summary>
[RegisterComponent]
public sealed partial class GeneticDataDiskComponent : Component
{
    [DataField]
    public int MaxMutations = 6;

    [DataField]
    public bool ReadOnly;

    [ViewVariables]
    public List<StoredMutation> Mutations = new();

    [ViewVariables]
    public GeneticMakeupData? Makeup;
}
