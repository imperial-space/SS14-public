using Content.Shared.Stacks;
using Robust.Shared.Map.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Prototypes;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Random;
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology;

/// <summary>Снаряд кровавого охладителя.</summary>
[RegisterComponent]
public sealed partial class SlimeBloodChillProjectileComponent : Component;
