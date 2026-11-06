using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Продвинутая пластическая хирургия: копирует облик и голос с одного человека на другого.</summary>
[RegisterComponent]
public sealed partial class UplinkPlasticSurgeryComponent : Component
{
    [ViewVariables]
    public EntityUid? Template;

    [ViewVariables]
    public string? TemplateName;

    [DataField]
    public TimeSpan SurgeryInteractDelay = TimeSpan.FromSeconds(10);
}
