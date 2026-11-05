using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

// ───── DoAfter ─────

[Serializable, NetSerializable]
public sealed partial class CultScribeRuneDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public string Rune = string.Empty;

    [DataField]
    public string? Keyword;
}
