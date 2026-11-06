using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>Рука заклинания коснулась цели: эффект касания (cast_on_hand_hit).</summary>
[ByRefEvent]
public record struct ImperialTouchHitEvent(EntityUid Caster, EntityUid Target, EntityUid Hand)
{
    /// <summary>true — заклинание сработало, рука исчезает, начинается откат.</summary>
    public bool Success;
}
