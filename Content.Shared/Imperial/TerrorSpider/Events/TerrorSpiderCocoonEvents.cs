using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider.Events;

public sealed partial class TerrorSpiderCocoonActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class TerrorSpiderCocoonDoAfterEvent : SimpleDoAfterEvent;
