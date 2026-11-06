using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

[Serializable, NetSerializable]
public sealed class ImperialModularShieldGeneratorState : BoundUserInterfaceState
{
    public float MaxRadius;
    public float CurrentRadius;
    public float MaxStrength;
    public float CurrentStrength;
    public float MaxRegeneration;
    public float CurrentRegeneration;
    public bool Active;
    public bool Recovering;
    public bool ExteriorOnly;
    public bool Initiating;
}
