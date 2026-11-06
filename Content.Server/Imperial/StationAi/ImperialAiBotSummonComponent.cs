using System.Linq;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Power.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi;

/// <summary>call_bot(): бот идёт к точке, потом возвращается к своим делам.</summary>
[RegisterComponent]
public sealed partial class ImperialAiBotSummonComponent : Component
{
    [ViewVariables]
    public HTNCompoundTask? OldRoot;

    [ViewVariables]
    public EntityCoordinates Target;

    [ViewVariables]
    public TimeSpan Until;
}
