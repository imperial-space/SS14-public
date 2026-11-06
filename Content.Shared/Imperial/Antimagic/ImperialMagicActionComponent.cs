using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Antimagic;

/// <summary>
/// Действие — заклинание еретика или культа. Его нельзя применить, держа антимагию,
/// а заклинание с целью не действует на цель под защитой.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialMagicActionComponent : Component
{
    [DataField]
    public ImperialMagicResistance Resistance = ImperialMagicResistance.Magic;
}
