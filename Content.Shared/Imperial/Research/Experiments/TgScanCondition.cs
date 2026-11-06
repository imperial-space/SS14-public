using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>Особые условия сканирования, проверяемые кодом.</summary>
public enum TgScanCondition : byte
{
    /// <summary>scanning/people/mutant: живой гуманоид с активной мутацией.</summary>
    Mutant,
    /// <summary>scanning/people/augmented_organs: гуманоид с кибернетическим органом.</summary>
    AugmentedOrgans,
    /// <summary>scanning/people/android: полностью аугментированный гуманоид.</summary>
    Android,
    /// <summary>scanning/people/skillchip: гуманоид со скилл-чипом.</summary>
    Skillchip,
    /// <summary>scanning/random/mecha_equipped_scan: мех с оборудованием.</summary>
    MechEquipped,
    /// <summary>scanning/random/mecha_damage_scan: повреждённый мех.</summary>
    MechDamaged,
    /// <summary>scanning/random/plants/wild: мутировавшее растение в лотке.</summary>
    PlantMutated,
    /// <summary>scanning/random/plants/traits: растение с особой чертой.</summary>
    PlantTrait,
    /// <summary>autopsy/human: труп человека.</summary>
    AutopsyHuman,
    /// <summary>autopsy/nonhuman: труп гуманоида не-человека.</summary>
    AutopsyNonHuman,
    /// <summary>autopsy/xenomorph: труп ксеноморфа.</summary>
    AutopsyXeno,
    /// <summary>machinery_tiered_scan: работающая машина, собранная из деталей.</summary>
    Machine,
}
