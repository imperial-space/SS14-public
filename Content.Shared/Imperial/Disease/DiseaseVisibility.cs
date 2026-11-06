using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

/// <summary>HIDDEN_*: от чего болезнь скрыта.</summary>
[Flags, Serializable, NetSerializable]
public enum DiseaseVisibility : byte
{
    None = 0,

    /// <summary>Не видна анализатору здоровья.</summary>
    Scanner = 1 << 0,

    /// <summary>Не видна в PanD.E.M.I.C.</summary>
    Pandemic = 1 << 1,
}
