using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>Плазмоцил (plasmocile): плазма в крови не отравляет.</summary>
[RegisterComponent]
public sealed partial class GeneticPlasmocileComponent : Component
{
    [DataField]
    public string Reagent = "Plasma";
}
