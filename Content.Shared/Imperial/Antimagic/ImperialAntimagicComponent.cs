using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Antimagic;

/// <summary>
/// Предмет даёт антимагию тому, кто держит его в руках или носит (anti_magic у nullrod_core).
/// Носитель не может сам колдовать магию, от которой защищён.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialAntimagicComponent : Component
{
    [DataField]
    public ImperialMagicResistance Resistance = ImperialMagicResistance.All;
}
