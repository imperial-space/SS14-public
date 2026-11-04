using System.Numerics;
using Content.Server.Imperial.Antimagic;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Antimagic;
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

/// <summary>Эффект заклинания: поднимается на действии, обрабатывают компоненты эффекта.</summary>
[ByRefEvent]
public record struct ImperialSpellEffectEvent(EntityUid Caster, EntityUid? Target, EntityCoordinates? Coords)
{
    public bool Handled;
}
