using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.TerrorSpider.Events;

public sealed partial class TerrorSpiderSpawnWebActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId WebPrototype;
}
