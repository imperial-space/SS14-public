using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Components;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class StoredMutation
{
    [DataField]
    public ProtoId<GeneticMutationPrototype> Mutation;

    [DataField]
    public ChromosomeKind Chromosome = ChromosomeKind.None;
}
