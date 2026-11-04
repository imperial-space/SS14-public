using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Blob.Components;

/// <summary>Флаги урона SS13 (damage_flag) — от них зависят броня и реакции штамма.</summary>
public enum BlobDamageFlag : byte
{
    None,
    Melee,
    Bullet,
    Laser,
    Energy,
    Bomb,
    Fire,
    Acid,
}
