using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic;

[Serializable, NetSerializable]
public enum HereticPath : byte
{
    General = 0,
    Ash     = 1,
    Moon    = 2,
    Lock    = 3,
    Flesh   = 4,
    Void    = 5,
    Blade   = 6,
    Rust    = 7,
    Cosmos  = 8,
}
