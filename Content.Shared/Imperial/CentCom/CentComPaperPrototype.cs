using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.CentCom;

[Prototype("centcomPaperMsg")]
public sealed partial class CentComPaperPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField("desc")] public string Desc { get; set; } = string.Empty;

    [DataField("content", required: true)] public string Content { get; set; } = default!;

    /// <summary>
    /// Если true, аргумент с текстом решения обязателен и подставляется в шаблон.
    /// </summary>
    [DataField("requiresDecision")] public bool RequiresDecision { get; set; }
}
