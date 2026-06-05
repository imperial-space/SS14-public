using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Melee weapon that marks targets on hit and deals bonus damage to marked targets.
/// Also deals backstab bonus damage when attacking from behind.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KineticCrusherComponent : Component
{
    /// <summary>
    /// Bonus damage applied when the target has <see cref="MarkedByKineticCrusherComponent"/>.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier MarkDamageBonus = new();

    /// <summary>
    /// Bonus damage applied when attacking from behind the target (backstab).
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier BackstabDamageBonus = new();

    /// <summary>
    /// How long the mark lasts after being applied.
    /// </summary>
    [DataField]
    public TimeSpan MarkDuration = TimeSpan.FromSeconds(8);

    /// <summary>
    /// If set, the crusher can only deal damage (base + bonus) to targets matching this whitelist.
    /// Entities not on the whitelist are removed from <see cref="MeleeHitEvent.HitEntities"/> before any processing.
    /// </summary>
    [DataField]
    public EntityWhitelist? TargetWhitelist;
}
