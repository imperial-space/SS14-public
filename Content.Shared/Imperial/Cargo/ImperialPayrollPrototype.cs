using Content.Shared.Cargo.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cargo;

/// <summary>
/// Зарплатная ведомость TG: paycheck и paycheck_department каждой должности (/datum/job),
/// payday_modifier рас (/datum/species).
/// </summary>
[Prototype]
public sealed partial class ImperialPayrollPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Ставки должностей. Должности, которых нет в списке, получают ставку по отделу.</summary>
    [DataField]
    public Dictionary<ProtoId<JobPrototype>, ImperialJobPay> Jobs = new();

    /// <summary>payday_modifier расы; по умолчанию 1.</summary>
    [DataField]
    public Dictionary<ProtoId<SpeciesPrototype>, float> Species = new();
}
