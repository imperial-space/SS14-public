using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Клиент выбрал набор в радиальном меню маяка вооружения.
/// </summary>
[Serializable, NetSerializable]
public sealed class HolyArmamentsPickMessage(EntProtoId kit) : BoundUserInterfaceMessage
{
    public readonly EntProtoId Kit = kit;
}
