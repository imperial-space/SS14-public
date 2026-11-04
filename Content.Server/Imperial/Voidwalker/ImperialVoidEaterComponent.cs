using System.Linq;
using Content.Server.Atmos.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Voidwalker;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>/obj/item/void_eater: временно разбивает стекло касанием.</summary>
[RegisterComponent]
public sealed partial class ImperialVoidEaterComponent : Component;
