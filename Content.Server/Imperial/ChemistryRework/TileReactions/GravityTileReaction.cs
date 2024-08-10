using Content.Server.Singularity.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Server.Chemistry.TileReactions;

/// <summary>
///     Creates a gravitational pulse, shoving around all entities within some distance of an epicenter.
/// </summary>

[DataDefinition]
public sealed partial class GravityTileReaction : ITileReaction
{
    [DataField("minRange")]
    public float MinRange = 0.0f;

    [DataField("maxRange", required: true)]
    public float MaxRange = default!;

    /// <summary>
    ///     The base radial velocity that will be added to entities within range towards the center of the gravitational pulse
    /// </summary>
    [DataField("baseRadialDeltaV")]
    public float BaseRadialDeltaV = default!;

    /// <summary>
    ///     The base tangential velocity that will be added to entities within countrclockwise around the center of the gravitational pulse.
    /// </summary>
    [DataField("baseTangentialDeltaV")]
    public float BaseTangentialDeltaV = default!;

    [DataField("impulsePerUnit")]
    public float ImpulsePerUnit = 0.1f;

    public FixedPoint2 TileReact(TileRef tile, ReagentPrototype reagent, FixedPoint2 reactVolume, IEntityManager entityManager, List<ReagentData>? data = null)
    {
        var gravityWellSys = entityManager.System<GravityWellSystem>();
        var trufSys = entityManager.System<TurfSystem>();
        var range = MathF.Min((float) (reactVolume * ImpulsePerUnit), MaxRange);

        gravityWellSys.GravPulse(
            trufSys.GetTileCenter(tile),
            range,
            MinRange,
            BaseRadialDeltaV,
            BaseTangentialDeltaV
        );

        return reactVolume;
    }
}
