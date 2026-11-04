using Content.Server.Chat.Managers;
using Content.Server.Electrocution;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Electrocution;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Voidwalker;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>/datum/dynamic_ruleset/midround/from_ghosts/voidwalker (и гелиарх).</summary>
[RegisterComponent]
public sealed partial class ImperialVoidwalkerRuleComponent : Component
{
    [DataField]
    public EntProtoId Spawner = "ImperialSpawnPointVoidwalker";
}
