using Robust.Shared.GameStates;

[RegisterComponent, NetworkedComponent]
public sealed partial class StaminaParryBoosterComponent : Component
{
    [DataField("staminaDamageMultiplier")]
    public float StaminaDamageMultiplier = 1.25f;
}
