using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

/// <summary>Руны культа (obj/effect/rune/*).</summary>
[Serializable, NetSerializable]
public enum CultRuneType : byte
{
    Malformed,
    Offer,
    Empower,
    Teleport,
    NarSie,
    Revive,
    Barrier,
    Summon,
    BloodBoil,
    SpiritRealm,
    Apocalypse,
}
