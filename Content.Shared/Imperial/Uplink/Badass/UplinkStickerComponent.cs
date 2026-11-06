using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Стикер Синдиката (sticker/syndicate): клеится на что угодно.</summary>
[RegisterComponent]
public sealed partial class UplinkStickerComponent : Component
{
    /// <summary>Наклеенная версия стикера.</summary>
    [DataField(required: true)]
    public EntProtoId Attached;
}
