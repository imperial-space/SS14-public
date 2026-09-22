using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Decals;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Decals;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRustPassiveSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem           _damageable        = default!;
    [Dependency] private readonly SolutionContainerSystem    _solutionContainer = default!;
    [Dependency] private readonly SharedTransformSystem      _xform             = default!;
    [Dependency] private readonly EntityLookupSystem         _lookup            = default!;
    [Dependency] private readonly DecalSystem                _decal             = default!;
    [Dependency] private readonly IGameTiming                _gameTiming        = default!;

    private const string RustDecalId = "Rust";

    public void ApplyPassiveLevel1(EntityUid uid)
    {
        EnsureComp<HereticRustPassiveComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<HereticRustPassiveComponent, HereticComponent>();

        while (query.MoveNext(out var uid, out var rustComp, out var heretic))
        {
            if (now < rustComp.NextTick)
                continue;

            rustComp.NextTick = now + rustComp.TickInterval;

            if (!IsOnRust(uid))
                continue;

            var healAmount = heretic.PassiveLevel switch
            {
                1 => FixedPoint2.New(-3),
                2 => FixedPoint2.New(-6),
                _ => FixedPoint2.New(-10),
            };

            var heal = new DamageSpecifier();
            heal.DamageDict["Blunt"]    = healAmount;
            heal.DamageDict["Slash"]    = healAmount;
            heal.DamageDict["Piercing"] = healAmount;
            _damageable.TryChangeDamage(uid, heal, true);

            if (heretic.PassiveLevel == 1)
                PurgeMetabolites(uid);
        }
    }

    private void PurgeMetabolites(EntityUid uid)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        _solutionContainer.ResolveSolution(uid, bloodstream.MetabolitesSolutionName,
            ref bloodstream.MetabolitesSolution, out _);

        if (bloodstream.MetabolitesSolution != null)
            _solutionContainer.RemoveAllSolution(bloodstream.MetabolitesSolution.Value);
    }

    private bool IsOnRust(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;
        var gridUid = _xform.GetGrid(coords);
        if (gridUid is not { } grid) return false;
        var snapped = coords.SnapToGrid(EntityManager);
        foreach (var (_, decal) in _decal.GetDecalsInRange(grid, snapped.Position))
        {
            if (decal.Id == RustDecalId) return true;
        }
        var mapCoords = _xform.ToMapCoordinates(snapped);
        return _lookup.GetEntitiesInRange<HereticRustOverlayComponent>(mapCoords, 0.4f).Count > 0;
    }
}
