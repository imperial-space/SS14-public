using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Fugitives;
using Content.Shared.Implants;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fugitives;

/// <summary>Стабилизированный Сатурн-X: невидимость до момента <see cref="Until"/>.</summary>
[RegisterComponent]
public sealed partial class ImperialInvisibilityComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}
