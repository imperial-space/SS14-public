using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Детонатор Синдиката: все взведённые бомбы Синдиката взрываются через 5 секунд.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndicateDetonatorComponent : Component
{
    [ViewVariables]
    public TimeSpan ReadyAt;
}
