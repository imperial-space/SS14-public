using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// item/kinetic_crusher: метит крупных существ дестабилизатором (ПКМ), удар по метке её подрывает.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KineticCrusherComponent : Component
{
    /// <summary>charge_time без трофеев, секунды.</summary>
    [DataField]
    public TimeSpan ChargeTime = TimeSpan.FromSeconds(1.5f);

    /// <summary>detonation_damage.</summary>
    [DataField]
    public float DetonationDamage = 50f;

    /// <summary>backstab_bonus.</summary>
    [DataField]
    public float BackstabBonus = 30f;

    /// <summary>crusher_mark duration.</summary>
    [DataField]
    public TimeSpan MarkDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public string ContainerId = "crusher_trophies";

    [DataField]
    public SoundSpecifier? BackstabSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/kinetic_accel.ogg");

    [DataField]
    public SoundSpecifier? InsertSound = new SoundPathSpecifier("/Audio/Items/screwdriver.ogg");

    /// <summary>temp_visual/kinetic_blast.</summary>
    [DataField]
    public EntProtoId DetonationEffect = "BulletImpactEffectKinetic";

    [DataField]
    public TimeSpan DeadlyShotResetDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ExpiresDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan Cooldown2 = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan MarkDetonationExtraWolfEarDuration = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan MarkDetonationExtraBearPawDelay = TimeSpan.FromSeconds(0.1);

    [DataField]
    public TimeSpan MarkDetonationExtraIceBlockTalismanDuration = TimeSpan.FromSeconds(4);
}
