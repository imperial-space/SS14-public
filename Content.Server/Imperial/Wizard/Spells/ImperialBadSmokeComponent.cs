using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/obj/effect/particle_effect/fluid/smoke/bad: душащий дым.</summary>
[RegisterComponent]
public sealed partial class ImperialBadSmokeComponent : Component;
