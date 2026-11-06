using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

[Serializable, NetSerializable]
public sealed class ImperialModularShieldConsoleState(List<ImperialModularShieldConsoleEntry> generators) : BoundUserInterfaceState
{
    public readonly List<ImperialModularShieldConsoleEntry> Generators = generators;
}
