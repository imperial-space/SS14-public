using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics.Prototypes;

[Flags, Serializable, NetSerializable]
public enum ChromosomeKind : byte
{
    None = 0,

    /// <summary>Нестабильность −20%.</summary>
    Stabilizer = 1 << 0,

    /// <summary>Отдача и побочные эффекты −50%.</summary>
    Synchronizer = 1 << 1,

    /// <summary>Сила мутации +50%.</summary>
    Power = 1 << 2,

    /// <summary>Откат способностей −50%.</summary>
    Energy = 1 << 3,
}
