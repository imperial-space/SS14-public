using System.Linq;
using Content.Shared.Chat;
using Content.Server.Construction.Components;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Climbing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Changeling;

/// <summary>Оглушён резонирующим криком: не слышит чужую речь до ExpiresAt.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingDeafComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}
