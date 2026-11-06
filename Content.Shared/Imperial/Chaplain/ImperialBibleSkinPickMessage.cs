using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Верховный жрец выбрал обложку библии в радиальном меню.
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialBibleSkinPickMessage(string skin) : BoundUserInterfaceMessage
{
    public readonly string Skin = skin;
}
