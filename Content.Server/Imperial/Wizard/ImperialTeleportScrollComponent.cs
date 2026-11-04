using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>/obj/item/teleportation_scroll: свиток с несколькими телепортами по зонам станции.</summary>
[RegisterComponent]
public sealed partial class ImperialTeleportScrollComponent : Component
{
    [DataField]
    public int Uses = 4;
}
