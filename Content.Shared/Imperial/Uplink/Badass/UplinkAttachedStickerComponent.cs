using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Наклеенный стикер; помнит, каким предметом был.</summary>
[RegisterComponent]
public sealed partial class UplinkAttachedStickerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Item;
}
