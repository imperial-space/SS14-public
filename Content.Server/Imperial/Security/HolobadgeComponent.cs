using System.Linq;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Blocking;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Server.GameTicking;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>clothing/accessory/holobadge.</summary>
[RegisterComponent]
public sealed partial class HolobadgeComponent : Component
{
    [ViewVariables]
    public string? StoredName;

    [ViewVariables]
    public bool Emagged;
}
