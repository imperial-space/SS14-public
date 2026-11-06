using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Автоинъектор, превращающий в моба (сыворотка магиллитиса).</summary>
[RegisterComponent]
public sealed partial class UplinkPolymorphInjectorComponent : Component
{
    [DataField(required: true)]
    public string Polymorph = string.Empty;
}
