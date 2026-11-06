using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[DataDefinition]
public sealed partial class ImperialAquariumStartFillEntry
{
    [DataField]
    public float Weight = 1;

    [DataField]
    public List<Robust.Shared.Prototypes.EntProtoId> Contents = new();

    [DataField]
    public string? Reagent;

    [DataField]
    public float Amount;
}
