using Content.Server.Popups;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Оружие Кадии: подрыв масс-реактивных снарядов, зарядка обойм на станции, перегрев плазмы.</summary>
public sealed class CadiaWeaponSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    private readonly HashSet<Entity<DamageableComponent>> _hit = new();
    private TimeSpan _nextCharge;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaMassReactiveComponent, ProjectileHitEvent>(OnBoltHit);
        SubscribeLocalEvent<CadiaGetsHotComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<CadiaEnergyPackComponent, ExaminedEvent>(OnPackExamined);
    }

    // ═══════════════════════ Подрыв снаряда ═══════════════════════

    private void OnBoltHit(Entity<CadiaMassReactiveComponent> ent, ref ProjectileHitEvent args)
    {
        var struck = args.Target;
        var coords = _xform.GetMapCoordinates(struck);
        _hit.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Radius, _hit);

        foreach (var target in _hit)
        {
            if (target.Owner == args.Shooter)
                continue;
            var other = _xform.GetMapCoordinates(target);
            // Цель, в которую снаряд попал, получает всё; остальных прикрывает укрытие и расстояние.
            var factor = 1f;
            if (target.Owner != struck)
            {
                var victim = target.Owner;
                if (!_interaction.InRangeUnobstructed(coords, other, ent.Comp.Radius + 0.1f, CollisionGroup.Impassable,
                        e => e == struck || e == victim))
                    continue;
                var share = Math.Clamp((other.Position - coords.Position).Length() / ent.Comp.Radius, 0f, 1f);
                factor = 1f - (1f - ent.Comp.EdgeFactor) * share;
            }
            _damage.TryChangeDamage(target.Owner, ent.Comp.Blast * factor, origin: args.Shooter);
        }

        Spawn(ent.Comp.Effect, coords);
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, args.Target);
    }

    // ═══════════════════════ Перегрев ═══════════════════════

    private void OnGunShot(Entity<CadiaGetsHotComponent> ent, ref GunShotEvent args)
    {
        if (!_random.Prob(ent.Comp.Chance))
            return;
        _damage.TryChangeDamage(args.User, ent.Comp.Burn);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-weapon-gets-hot", ("gun", Name(ent))), args.User, args.User, PopupType.MediumCaution);
    }

    // ═══════════════════════ Зарядка ═══════════════════════

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextCharge)
            return;
        _nextCharge = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<CadiaPackChargerComponent>();
        while (query.MoveNext(out var uid, out var charger))
        {
            foreach (var slot in charger.Slots)
            {
                if (!_container.TryGetContainer(uid, slot, out var container))
                    continue;
                foreach (var pack in container.ContainedEntities)
                {
                    if (!TryComp<BallisticAmmoProviderComponent>(pack, out var ammo) || ammo.Count >= ammo.Capacity)
                        continue;
                    var charged = Math.Min(ammo.Capacity - ammo.Container.ContainedEntities.Count, ammo.UnspawnedCount + charger.ChargePerSecond);
                    _gun.SetBallisticUnspawned((pack, ammo), charged);
                    if (ammo.Count >= ammo.Capacity)
                        _audio.PlayPvs(charger.DoneSound, uid);
                }
            }
        }
    }

    private void OnPackExamined(Entity<CadiaEnergyPackComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<BallisticAmmoProviderComponent>(ent, out var ammo))
            args.PushMarkup(Loc.GetString("cadia-pack-examine", ("shots", ammo.Count), ("max", ammo.Capacity)));
    }
}
