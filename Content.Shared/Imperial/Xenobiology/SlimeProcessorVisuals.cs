using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>Анимация обработки в процессоре слаймов.</summary>
[Serializable, NetSerializable]
public enum SlimeProcessorVisuals : byte
{
    Processing,
}
