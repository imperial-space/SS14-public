using Robust.Shared.Random;
using System.Linq;
using System.Numerics;
using Content.Server.Body;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Temperature.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Gravity;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Quirks;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.StepTrigger.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Body.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.Imperial.Quirks;

/// <summary>TRAIT_KEEN_NOSE: запах открытой ёмкости в руке выдаёт содержимое.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkKeenNoseComponent : Component;
