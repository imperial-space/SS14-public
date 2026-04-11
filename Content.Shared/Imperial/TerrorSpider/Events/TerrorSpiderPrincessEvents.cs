using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.TerrorSpider.Events;

public sealed partial class TerrorSpiderPrincessHiveSenseActionEvent : InstantActionEvent;

public sealed partial class TerrorSpiderPrincessScreamActionEvent : InstantActionEvent;

public sealed partial class TerrorSpiderPrincessLayEggActionEvent : InstantActionEvent
{
	[DataField(required: true)]
	public EntProtoId EggPrototype;
}
