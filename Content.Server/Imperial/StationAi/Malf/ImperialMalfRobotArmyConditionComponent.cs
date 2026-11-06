using System.Linq;
using Content.Server.Administration;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Console;
using Robust.Shared.Random;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>/datum/objective/robot_army: восемь активных киборгов, синхронизированных с ИИ.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfRobotArmyConditionComponent : Component
{
    [DataField]
    public int Required = 8;
}
