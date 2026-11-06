using Content.Server.AlertLevel;
using Content.Server.Power.Components;
using Content.Server.RoundEnd;
using Content.Shared.Access.Components;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>Ядро-укрытие внутри взломанного ЛКП (shunt).</summary>
[RegisterComponent]
public sealed partial class ImperialMalfShuntCoreComponent : Component
{
    [ViewVariables]
    public EntityUid Apc;

    /// <summary>Покинутое ядро, куда можно вернуться.</summary>
    [ViewVariables]
    public EntityUid Home;
}
