using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

[Serializable, NetSerializable]
public sealed class ImperialModularShieldConsoleEntry
{
    public int Id;
    public string Name = string.Empty;
    public float MaxStrength;
    public float CurrentStrength;
    public float CurrentRegeneration;
    public bool Active;
    public bool Recovering;
    public bool Inactive;
}
