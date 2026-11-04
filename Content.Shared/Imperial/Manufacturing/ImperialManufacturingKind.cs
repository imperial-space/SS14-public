using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

/// <summary>Вид машины производственной линии (/obj/machinery/power/manufacturing/*).</summary>
public enum ImperialManufacturingKind : byte
{
    Unloader,
    Crusher,
    Smelter,
    Lathe,
    Crafter,
    Router,
    Sorter,
    StorageBox,
}
