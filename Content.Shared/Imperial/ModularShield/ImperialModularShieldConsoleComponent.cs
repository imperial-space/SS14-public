using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

/// <summary>Консоль модульных щитов (/obj/machinery/computer/modular_shield).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModularShieldConsoleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Generators = new();
}
