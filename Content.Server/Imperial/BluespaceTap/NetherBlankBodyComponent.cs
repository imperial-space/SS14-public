using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.EntityTable;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.BluespaceTap;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BluespaceTap;

/// <summary>Пустое тело: при смерти отпускает поглощённого порталом.</summary>
[RegisterComponent]
public sealed partial class NetherBlankBodyComponent : Component
{
    public const string ContainerId = "held_body";
}
