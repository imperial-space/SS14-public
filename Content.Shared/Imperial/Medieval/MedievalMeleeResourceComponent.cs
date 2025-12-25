using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared.MedievalMeleeResource.Components;


[RegisterComponent, AutoGenerateComponentState, NetworkedComponent]
public sealed partial class MedievalMeleeResourceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Resource = 100f;

    [DataField]
    public float MaxResource = 107f;

    [DataField]
    public float ResourceWaste = 0.35f;

    [DataField, AutoNetworkedField]
    public string DamageState = "Full";

    [DataField]
    public SoundSpecifier? EffectSoundOnRepair = new SoundPathSpecifier("/Audio/Imperial/Medieval/repair_stone_use.ogg");

    [DataField]
    public SoundSpecifier? EffectSoundOnBreak = new SoundPathSpecifier("/Audio/Imperial/Medieval/melee_break.ogg");

    [DataField]
    public string SafeToHitGroup = "all";

    [DataField]
    public DamageSpecifier? BaseDamage;

    [DataField]
    public DamageSpecifier? BaseWieldBonus;

    [DataField]
    public float QualityMultiplier = 1f;


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
