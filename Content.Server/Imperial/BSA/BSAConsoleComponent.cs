using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Warps;
using Content.Shared.Imperial.BSA;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BSA;

/// <summary>Консоль управления (obj/machinery/computer/bsa_control).</summary>
[RegisterComponent]
public sealed partial class BSAConsoleComponent : Component
{
    /// <summary>Радиус поиска частей и готовой пушки (range(7)).</summary>
    [DataField]
    public float Range = 7f;

    [DataField]
    public EntProtoId CannonWest = "ImperialBSACannonWest";

    [DataField]
    public EntProtoId CannonEast = "ImperialBSACannonEast";

    [DataField]
    public ProtoId<AccessLevelPrototype> AuthAccess = "Command";

    /// <summary>Сколько ждать второго главу после первого прикладывания карты.</summary>
    [DataField]
    public TimeSpan AuthWindow = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public EntityUid? Cannon;

    [ViewVariables]
    public string? Notice;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public EntityUid? PendingCard;

    [ViewVariables]
    public EntityUid? PendingUser;

    [ViewVariables]
    public TimeSpan PendingUntil;

    [DataField]
    public TimeSpan DeployDuration = TimeSpan.FromSeconds(4f);
}
