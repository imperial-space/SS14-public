using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider;

[Serializable, NetSerializable]
public sealed partial class TerrorSpiderWrapDoAfterEvent : SimpleDoAfterEvent;
