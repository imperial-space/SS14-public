using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server.MedievalMeleeResource.Components;


[RegisterComponent]
public sealed partial class MedievalMeleeResourceComponent : Component
{
    [DataField]
    public float Resource = 100f;

    [DataField]
    public float MaxResource = 107f;

    [DataField]
    public float ResourceWaste = 0.35f;

    [DataField]
    public string DamageState = "Full";

    [DataField]
    public SoundSpecifier? EffectSoundOnRepair;

    [DataField]
    public SoundSpecifier? EffectSoundOnBreak;

    [DataField]
    public string SafeToHitGroup = "all";

    // Full
    // AlmostFull
    // Damaged
    // BadlyDamaged
    // Broken
    [DataField]
    public float UpModifier = 1.1f;
    [DataField]
    public float FullModifier = 1f;

    [DataField]
    public float AlmostFullModifier = 0.9f;

    [DataField]
    public float DamagedModifier = 0.75f;

    [DataField]
    public float BadlyDamagedModifier = 0.55f;

    [DataField]
    public float BrokenModifier = 0.35f;

    [DataField]
    public DamageSpecifier UpDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier FullDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier AlmostFullDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier DamagedDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier BadlyDamagedDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier BrokenDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier UpWieldDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier FullWieldDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier AlmostFullWieldDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier DamagedWieldDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier BadlyDamagedWieldDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };

    [DataField]
    public DamageSpecifier BrokenWieldDamage = new()
    {
        DamageDict = new()
        {
            { "Piercing", 1 },
        }
    };


}
