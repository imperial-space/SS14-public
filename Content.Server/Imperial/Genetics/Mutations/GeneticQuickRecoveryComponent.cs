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

/// <summary>Быстрое восстановление (quick recovery): оглушение вдвое короче.</summary>
[RegisterComponent]
public sealed partial class GeneticQuickRecoveryComponent : Component
{
    [DataField]
    public float StunMultiplier = 0.5f;
}
