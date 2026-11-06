using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider;

/// <summary>Смена icon_state: серый паук на паутине (terror_gray_cloaked), окрас принцессы (terror_princess1-3).</summary>
[Serializable, NetSerializable]
public enum TerrorSpiderVisuals : byte
{
    Stage,
}
