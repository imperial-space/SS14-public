using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping
{
    [Serializable, NetSerializable]
    public sealed partial class EnterVentCrawlerDoAfterEvent : SimpleDoAfterEvent
    {
    }
}