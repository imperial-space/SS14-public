using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.Power.Components;
using Content.Server.Silicons.Laws;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.StationAi;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Map;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>Модуль куплен — улучшения применяют свой эффект.</summary>
[ByRefEvent]
public readonly record struct ImperialMalfModuleBoughtEvent(EntityUid Ai, ImperialMalfModulePrototype Module);
