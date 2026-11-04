using Content.Shared.Cargo.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cargo;

[DataDefinition]
public sealed partial class ImperialJobPay
{
    /// <summary>PAYCHECK_LOWER = 25, PAYCHECK_CREW = 50, PAYCHECK_COMMAND = 100.</summary>
    [DataField]
    public int Paycheck = 50;

    /// <summary>Бюджет отдела, из которого платится зарплата. Без него счёт не открывается (как у ОБР в TG).</summary>
    [DataField]
    public ProtoId<CargoAccountPrototype>? Department;
}
