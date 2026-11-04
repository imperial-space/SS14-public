using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.ZLevels;
using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Timing;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Шаттл летит на соседний этаж: короткий взлёт или посадка вместо прыжка.</summary>
[RegisterComponent]
public sealed partial class CadiaVerticalFlightComponent : Component
{
    [ViewVariables]
    public bool Up;

    [ViewVariables]
    public CadiaFlightLevel To;
}
