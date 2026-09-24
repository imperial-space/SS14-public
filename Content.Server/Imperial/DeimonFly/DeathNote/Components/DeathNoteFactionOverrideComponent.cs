using Content.Shared.Damage;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Хранит исходные фракции цели, пока Тетрадь смерти временно делает её
/// враждебной всем станционным NPC.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteFactionOverrideComponent : Component
{
    [ViewVariables]
    public HashSet<ProtoId<NpcFactionPrototype>> OriginalFactions = new();

    [ViewVariables]
    public bool OwnsFactionComponent;

    [ViewVariables]
    public TimeSpan RestoreAt;

    /// <summary>
    /// Part of the configured total damage that has not yet been delivered.
    /// It is divided by the remaining hit count on every impact so fixed-point
    /// rounding cannot push the accumulated result over the configured maximum.
    /// </summary>
    [ViewVariables]
    public DamageSpecifier EnergyTurretDamageRemaining = new();

    /// <summary>
    /// Number of enhanced energy-turret hits still allowed for this fate.
    /// Once exhausted, later projectiles from the same burst deal no damage to
    /// the marked target and therefore cannot push the corpse over the limit.
    /// </summary>
    [ViewVariables]
    public int EnergyTurretHitsRemaining;

    /// <summary>
    /// Energy turrets explicitly ordered to treat the target as hostile.
    /// Required because their own AllHostile faction does not consider another
    /// AllHostile member a candidate.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> EnergyTurrets = new();
}
