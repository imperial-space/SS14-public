using Content.Shared.Cargo.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>ui_data торгомата: покупатель, деньги (счёт и наличные), отдел и реклама.</summary>
[Serializable, NetSerializable]
public sealed class ImperialVendingUserState(bool hasUser, string holder, string job, int cash, string? department, string ad, bool free) : BoundUserInterfaceState
{
    public readonly bool HasUser = hasUser;
    public readonly string Holder = holder;
    public readonly string Job = job;
    public readonly int Cash = cash;
    public readonly string? Department = department;
    public readonly string Ad = ad;
    public readonly bool Free = free;
}
