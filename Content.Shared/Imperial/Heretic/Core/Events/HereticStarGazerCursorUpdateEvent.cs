using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

// ─── Star Gazer — трекинг курсора (клиент → сервер) ─────────────────────────

[Serializable, NetSerializable]
public sealed class HereticStarGazerCursorUpdateEvent : EntityEventArgs
{
    public NetEntity Uid;
    public MapCoordinates Coords;
    public HereticStarGazerCursorUpdateEvent(NetEntity uid, MapCoordinates coords)
    {
        Uid = uid;
        Coords = coords;
    }
}
