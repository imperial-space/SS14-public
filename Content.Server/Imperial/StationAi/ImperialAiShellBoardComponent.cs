using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Lathe;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Research.Prototypes;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi;

/// <summary>/obj/item/borg/upgrade/ai: модуль Б.О.Р.И.С. вместо мозга делает корпус оболочкой ИИ.</summary>
[RegisterComponent]
public sealed partial class ImperialAiShellBoardComponent : Component;
