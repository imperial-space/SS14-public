using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.CentCom;

[Prototype("centcomAnnounceMsg")]
public sealed partial class CentComAnnouncePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField("desc")] public string Desc { get; set; } = string.Empty;

    [DataField("message", required: true)] public string Message { get; set; } = default!;
}
