using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Allows the KineticCrusher to accept upgrade items slotted into its upgrade container.
/// Each upgrade costs some capacity out of <see cref="MaxCapacity"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KineticCrusherUpgradeableComponent : Component
{
    /// <summary>Container id for installed upgrades.</summary>
    [DataField]
    public string UpgradesContainerId = "crusher_upgrades";

    /// <summary>Total upgrade capacity (sum of all SlotCosts must not exceed this).</summary>
    [DataField]
    public int MaxCapacity = 100;

    /// <summary>
    /// Base UseDelay value (seconds) before any Legion Skull reductions are applied.
    /// Must match the initial UseDelay.delay on the entity.
    /// </summary>
    [DataField]
    public float BaseUseDelay = 0.9f;

    /// <summary>The stock ammo prototype (before Watcher Wing replaces it).</summary>
    [DataField]
    public EntProtoId BaseAmmoProto = "BulletCharge";

    [DataField]
    public SoundSpecifier? InsertSound;
}
