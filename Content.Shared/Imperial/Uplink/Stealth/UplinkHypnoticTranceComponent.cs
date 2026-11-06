using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Транс: следующая услышанная фраза станет гипнотическим внушением.</summary>
[RegisterComponent]
public sealed partial class UplinkHypnoticTranceComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;

    [DataField]
    public TimeSpan ReminderDelay = TimeSpan.FromSeconds(60);
}
