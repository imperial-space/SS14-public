using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>DOMAIN_* флаги.</summary>
[Flags, Serializable, NetSerializable]
public enum BitrunningDomainFlags : byte
{
    None = 0,
    ForbidsItems = 1 << 0,
    ForbidsAbilities = 1 << 1,
    NoNohitBonus = 1 << 2,
    TestOnly = 1 << 3,
}
