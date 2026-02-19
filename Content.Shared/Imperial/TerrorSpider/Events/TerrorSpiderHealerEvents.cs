using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.TerrorSpider.Events;

public sealed partial class TerrorSpiderHealerPulseActionEvent : InstantActionEvent;

public sealed partial class TerrorSpiderHealerLayEggActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId EggPrototype;
}
