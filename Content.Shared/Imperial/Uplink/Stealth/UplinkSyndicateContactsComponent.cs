using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Контактные линзы Синдиката: защита от вспышек.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndicateContactsComponent : Component
{
    [DataField]
    public TimeSpan ContactsUseDelay = TimeSpan.FromSeconds(3);
}
