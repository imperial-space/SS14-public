using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

[Serializable, NetSerializable]
public enum ImperialMedScanMode : byte
{
    /// <summary>autopsy_scanner: отчёт о вскрытии трупа на бумаге.</summary>
    Autopsy,
    /// <summary>ph_meter: состав раствора в ёмкости.</summary>
    Chemical,
    /// <summary>blood_scanner: уровень крови и лекарства в ней.</summary>
    Blood,
    /// <summary>penlight: реакция зрачков.</summary>
    Pupils,
    /// <summary>reflexhammer: рефлексы и неврологический урон.</summary>
    Reflex,
}
