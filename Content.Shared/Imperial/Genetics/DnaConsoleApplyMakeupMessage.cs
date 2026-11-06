using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Перенести облик из буфера на пациента; без пациента — отложенно.</summary>
[Serializable, NetSerializable]
public sealed class DnaConsoleApplyMakeupMessage(int index, DnaMakeupType type) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
    public readonly DnaMakeupType Type = type;
}
