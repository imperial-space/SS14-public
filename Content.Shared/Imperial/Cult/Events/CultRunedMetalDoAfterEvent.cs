using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public sealed partial class CultRunedMetalDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public string Result = string.Empty;

    [DataField]
    public int Cost;
}
