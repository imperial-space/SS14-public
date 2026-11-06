using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Класс мутации пациента: из генома, добавлена сверху (MUT_EXTRA), из других источников (MUT_OTHER).</summary>
[Serializable, NetSerializable]
public enum DnaMutationClass : byte
{
    Normal,
    Extra,
    Other,
}
