using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

[DataDefinition]
public sealed partial class ImperialTgCraftItem
{
    /// <summary>Подходящие прототипы (с учётом наследников).</summary>
    [DataField]
    public List<EntProtoId> Protos = new();

    [DataField]
    public ProtoId<TagPrototype>? Tag;

    [DataField]
    public int Count = 1;

    /// <summary>Ключ локализации названия требования для подсказки.</summary>
    [DataField(required: true)]
    public LocId Name;
}
