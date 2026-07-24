using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote;

/// <summary>
/// Небольшие типы обработчиков, поддерживаемые реестром пресетов.
/// </summary>
[Serializable, NetSerializable]
public enum DeathNotePresetHandlerType : byte
{
    HeartAttack,
    ImmovableRod,
    Meteor,
    Explosion,
    Electrocution,
    Fire,
    Poison,
    Asphyxiation,
    Fauna,
    DirectDamage,
    Lightning,
    HostileFaction,
    GuidedAirlock,
    GuidedDisposal,
    GuidedVending,
    GuidedFoodPoisoning,
    GuidedDrinkPoisoning,
    CeilingCollapse,
    Mimic,
    BluespaceAnomaly,
}
