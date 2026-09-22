using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRustSmokeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRustSmokeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HereticRustSmokeComponent> ent, ref MapInitEvent args)
    {
        var mapCoords = _xform.GetMapCoordinates(ent.Owner);

        if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(mapCoords, 0.4f, LookupFlags.Static).Count == 0)
            Spawn("HereticRustOverlay", mapCoords);

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(mapCoords, 0.6f))
        {
            if (!HasComp<BorgChassisComponent>(mob.Owner)) continue;
            if (!_mobs.IsAlive(mob.Owner, mob.Comp)) continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Blunt"] = FixedPoint2.New(ent.Comp.BorgDamage);
            _damage.TryChangeDamage(mob.Owner, dmg, ignoreResistances: true);
        }
    }
}
