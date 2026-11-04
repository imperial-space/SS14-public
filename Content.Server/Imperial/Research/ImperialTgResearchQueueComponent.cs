using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Research.Components;
using Content.Server.Research.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Imperial.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Research;

/// <summary>Очередь изучения сервера techweb TG (queue_nodes).</summary>
[RegisterComponent]
public sealed partial class ImperialTgResearchQueueComponent : Component
{
    [ViewVariables]
    public List<(string Node, string By, EntityUid Console, EntityUid User)> Queue = new();
}
