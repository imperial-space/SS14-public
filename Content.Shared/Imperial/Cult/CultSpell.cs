using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

/// <summary>Заклинания магии крови (datum/action/innate/cult/blood_spell/*).</summary>
[Serializable, NetSerializable]
public enum CultSpell : byte
{
    Stun,
    Teleport,
    Emp,
    Shackles,
    Construction,
    Equipment,
    Dagger,
    Horror,
    Veiling,
    BloodRites,
}
