using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.SpectralBlade;

/// <summary>
/// Spectral blade that ghosts can infuse to increase its damage.
/// Each infusion from a ghost adds ChargeStep to the base damage.
/// Max charges = MaxDamage / ChargeStep.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpectralBladeComponent : Component
{
    /// <summary>Base melee damage at zero charges.</summary>
    [DataField]
    public float BaseDamage = 1f;

    /// <summary>How much damage each ghost infusion adds.</summary>
    [DataField]
    public float ChargeStep = 4f;

    /// <summary>Maximum total damage (achieved at max charges).</summary>
    [DataField]
    public float MaxDamage = 76f;

    /// <summary>Current number of ghost infusions.</summary>
    [DataField, AutoNetworkedField]
    public int ChargeLevel = 0;

    /// <summary>Cooldown between infusions from the same ghost (seconds).</summary>
    [DataField]
    public float InfusionCooldown = 60f;

    /// <summary>Last ghost infusion time per ghost entity UID (not networked, server-only).</summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> InfusionHistory = new();
}
