using Robust.Shared.Prototypes;
using Content.Shared.Body;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Данные <see cref="CultSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class CultOriginalEyesComponent : Component
{
    [ViewVariables]
    public Dictionary<ProtoId<Content.Shared.Body.OrganCategoryPrototype>, Content.Shared.Body.OrganProfileData> Value;
}
