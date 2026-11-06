using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Действия «подняться/опуститься на этаж», выданные призраку, ИИ или летящему на джетпаке.</summary>
[RegisterComponent]
public sealed partial class ZFlyActionsComponent : Component
{
    [ViewVariables]
    public EntityUid? Up;

    [ViewVariables]
    public EntityUid? Down;
}
