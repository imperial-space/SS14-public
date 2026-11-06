using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Components;

[DataDefinition]
public sealed partial class ActiveMutation
{
    [DataField]
    public MutationSource Sources;

    [DataField]
    public ChromosomeKind Chromosome = ChromosomeKind.None;

    /// <summary>Компоненты, которые добавила мутация (их и снимаем).</summary>
    [DataField]
    public List<string> AddedComponents = new();

    [ViewVariables]
    public List<EntityUid> ActionEntities = new();
}
