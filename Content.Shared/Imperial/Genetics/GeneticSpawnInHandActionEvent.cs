using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Создать предмет в руке (геладикинез — снежок, циндикинез — пепел).</summary>
public sealed partial class GeneticSpawnInHandActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}
