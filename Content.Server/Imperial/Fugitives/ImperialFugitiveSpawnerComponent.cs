using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fugitives;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Physics;
using Content.Shared.Roles;
using Content.Shared.Station;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Fugitives;

/// <summary>Гост-роль беглеца или охотника: что выдать при вселении.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveSpawnerComponent : Component
{
    [DataField]
    public bool Hunter;

    [DataField]
    public ImperialFugitiveBackstory Backstory;

    [DataField]
    public ImperialHunterPack Pack;

    [DataField]
    public ProtoId<StartingGearPrototype>? Gear;

    [DataField]
    public bool Leader;
}
