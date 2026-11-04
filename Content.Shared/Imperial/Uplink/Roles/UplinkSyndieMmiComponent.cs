using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>ИИМ Синдиката: борг с этим мозгом получает законы Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieMmiComponent : Component
{
    [DataField]
    public string Laws = "SyndicateStatic";
}
