using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Components;

[DataDefinition]
public sealed partial class AdvancedInjector
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public List<StoredMutation> Mutations = new();
}
