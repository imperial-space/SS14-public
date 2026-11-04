using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Сущность, на которую наклеены стикеры.</summary>
[RegisterComponent]
public sealed partial class UplinkStickeredComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Stickers = new();
}
