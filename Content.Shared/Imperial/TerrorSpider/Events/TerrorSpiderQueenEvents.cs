using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.TerrorSpider.Events;

public sealed partial class TerrorSpiderQueenCreateHiveActionEvent : InstantActionEvent;

public sealed partial class TerrorSpiderQueenScreamActionEvent : InstantActionEvent;

public sealed partial class TerrorSpiderQueenHiveCountActionEvent : InstantActionEvent;

public sealed partial class TerrorSpiderQueenLayEggActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId EggPrototype;

    [DataField]
    public string? SharedCooldownKey;

    [DataField]
    public float SharedCooldownSeconds;

    [DataField]
    public string? RoyalCooldownKey;
}
