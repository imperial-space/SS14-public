using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

/// <summary>«fuel»: выбор рецепта, null — «Nothing».</summary>
[Serializable, NetSerializable]
public sealed class HypertorusSelectFuelMessage(string? fuel) : BoundUserInterfaceMessage
{
    public readonly string? Fuel = fuel;
}
