using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticVoidWeaveComponent : Component
{
    /// <summary>EntityUid of the current wearer, set on equip, cleared on unequip.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;

    /// <summary>Probability (0–1) that an incoming attack is completely nullified.</summary>
    [DataField]
    public float BlockChance = 0.3f;

    /// <summary>How long the stealth effect lasts after a block.</summary>
    [DataField]
    public TimeSpan StealthDuration = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public bool IsStealthed;

    [DataField]
    public TimeSpan StealthEndTime;
}
