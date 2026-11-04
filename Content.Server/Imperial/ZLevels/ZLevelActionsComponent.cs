using System.Linq;
using Content.Server.Station.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Gravity;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Действия «посмотреть вверх/вниз», выданные игроку, пока он на многоэтажной карте.</summary>
[RegisterComponent]
public sealed partial class ZLevelActionsComponent : Component
{
    [ViewVariables]
    public EntityUid? LookUp;

    [ViewVariables]
    public EntityUid? LookDown;
}
