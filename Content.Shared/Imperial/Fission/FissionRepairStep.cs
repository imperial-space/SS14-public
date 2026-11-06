using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

/// <summary>Шаги ремонта расплавленного реактора (REACTOR_NEEDS_*).</summary>
[Serializable, NetSerializable]
public enum FissionRepairStep : byte
{
    Digging = 1,
    Crowbar = 2,
    Plastitanium = 3,
    Wrench = 4,
    Welding = 5,
    Plasteel = 6,
    Screwdriver = 7,
}
