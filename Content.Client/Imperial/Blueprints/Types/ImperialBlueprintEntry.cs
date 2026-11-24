using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Blueprints.Types;

/// <summary>Одна постройка чертежа: рецепт, клетка относительно левого нижнего угла (в осях сетки) и направление.</summary>
[DataDefinition]
public sealed partial class ImperialBlueprintEntry
{
    [DataField(required: true)]
    public ProtoId<ConstructionPrototype> Construction;

    [DataField]
    public int X;

    [DataField]
    public int Y;

    /// <summary>Направление постройки относительно сетки.</summary>
    [DataField]
    public Direction Dir = Direction.South;
}
