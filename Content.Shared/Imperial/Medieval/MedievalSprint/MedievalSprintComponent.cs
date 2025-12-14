using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Sprint;


[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalSprintComponent : Component
{
    /// <summary>
    /// Stamina damage by period
    /// </summary>
    [DataField]
    public TimeSpan StaminaGainPeriod = TimeSpan.FromSeconds(0.1f);

    /// <summary>
    /// If the damage to stamina is greater than this value, then we stop running.
    /// </summary>
    [DataField]
    public float MinStaminaToSprintPrecent = 0.85f;

    /// <summary>
    /// Stamina damge for <see cref="StaminaGainPeriod" />
    /// </summary>
    [DataField]
    public float StaminaDamage = 0.75f;

    /// <summary>
    /// Speed sprint if StaminaDamage less when <see cref="MinStaminaToSprintPrecent" />
    /// </summary>
    [DataField]
    public float SprintSpeedModifierWhenTried = 0.5f;


    [ViewVariables]
    public TimeSpan NextStaminaDamageTime = TimeSpan.Zero;

    [ViewVariables]
    public bool Tried = false;

    public bool Sprinting = false;
}
