using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Myrmex;

[Prototype]
public sealed partial class LarvaGrowPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField] public List<EntProtoId> Recipe { get; private set; } = [];
    [DataField] public GrowCondition? Condition { get; private set; }
    [DataField] public EntProtoId ResultEntity { get; private set; }
    [DataField] public int Priority { get; private set; } = 1;
}

// оно не хочет работать...
// [ImplicitDataDefinitionForInheritors]
// public abstract partial class GrowCondition
// {
//     public abstract bool Check(List<string> input);
// }
//
// public sealed partial class ConditionNonEquals : GrowCondition
// {
//     [DataField] public int First;
//     [DataField] public int Second;
//
//     public override bool Check(List<string> input)
//         => input[First] != input[Second];
// }
[DataDefinition]
public sealed partial class GrowCondition
{
    [DataField] public int A;
    [DataField] public int B;
    [DataField] public EntProtoId Except;

    public bool Check(List<string> input)
        => (input[A] != input[B]) || (input[A] == Except && input[B] == Except);
}
