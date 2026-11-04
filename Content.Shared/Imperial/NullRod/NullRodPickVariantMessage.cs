using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.NullRod;

/// <summary>
/// Клиент выбрал форму святого оружия в радиальном меню нулевого стержня.
/// </summary>
[Serializable, NetSerializable]
public sealed class NullRodPickVariantMessage(EntProtoId variant) : BoundUserInterfaceMessage
{
    public readonly EntProtoId Variant = variant;
}
