using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.ErtCall;
[Serializable, Prototype("ertCall")]
public sealed class ErtCallPresetPrototype : IPrototype
{
    [IdDataFieldAttribute] public string ID { get; } = default!;

    [DataField("path")] public string Path { get; set; } = string.Empty;

    [DataField("desc")] public string Desc { get; set; } = string.Empty;
}
