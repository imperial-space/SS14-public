using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

/// <summary>Ручки интерфейса (ui_act в hfr_parts.dm).</summary>
[Serializable, NetSerializable]
public enum HypertorusParameter : byte
{
    HeatingConductor,
    MagneticConstrictor,
    CurrentDamper,
    FuelInjectionRate,
    ModeratorInjectionRate,
    CoolingVolume,
    ModeratorFilteringRate,
}
