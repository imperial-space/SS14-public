using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Cult.Components;

[DataDefinition]
public sealed partial class CultDispenserOption
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public List<EntProtoId> Items = new();

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;
}
