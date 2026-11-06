using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

/// <summary>Флаги болезни: лечится ли, можно ли быть носителем, вырабатывается ли иммунитет.</summary>
[Flags, Serializable, NetSerializable]
public enum DiseaseFlags : byte
{
    None = 0,
    Curable = 1 << 0,
    CanCarry = 1 << 1,
    CanResist = 1 << 2,

    /// <summary>Хроническая: лекарство лишь откатывает стадию.</summary>
    Chronic = 1 << 3,

    /// <summary>Лекарство снимает по одной стадии, а не лечит сразу.</summary>
    IncrementalCure = 1 << 4,
}
