using Content.Server.Body.Systems;
using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Speech.Muting;
using Content.Shared.Spider;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderWidowSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private TimeSpan _nextVenomTick = TimeSpan.Zero;
    private static readonly TimeSpan VenomTickInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderWidowMeleeDebuffComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<TerrorSpiderWidowWebAreaComponent, StartCollideEvent>(OnWebStartCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextVenomTick)
            return;

        _nextVenomTick = _timing.CurTime + VenomTickInterval;

        var query = EntityQueryEnumerator<BloodstreamComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var venomAmount = _solution.GetTotalPrototypeQuantity(uid, "BlackTerrorVenom").Float();
            if (venomAmount <= 0f)
                continue;

            var toxinDamage = GetVenomDamageByVolume(venomAmount);
            if (toxinDamage <= 0f)
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict["Toxin"] = FixedPoint2.New(toxinDamage);
            _damageable.TryChangeDamage(uid, damage, ignoreResistances: false, interruptsDoAfters: false);
        }
    }

    private void OnMeleeHit(Entity<TerrorSpiderWidowMeleeDebuffComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var target in args.HitEntities)
        {
            if (TryComp<StatusEffectsComponent>(target, out var statusEffects))
            {
                _statusEffects.TryAddStatusEffect<MutedComponent>(
                    target,
                    "Muted",
                    TimeSpan.FromSeconds(ent.Comp.MuteDuration),
                    true,
                    statusEffects);
            }

            AddVenomToTarget(target, ent.Comp.VenomReagent, ent.Comp.VenomPerHit);
        }
    }

    private void OnWebStartCollide(Entity<TerrorSpiderWidowWebAreaComponent> ent, ref StartCollideEvent args)
    {
        if (HasComp<IgnoreSpiderWebComponent>(args.OtherEntity))
            return;

        AddVenomToTarget(args.OtherEntity, ent.Comp.VenomReagent, ent.Comp.VenomOnTouch);
    }

    private void AddVenomToTarget(EntityUid target, string reagentId, float amount)
    {
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        var solution = new Solution();
        solution.AddReagent(reagentId, FixedPoint2.New(amount));
        _bloodstream.TryAddToChemicals((target, bloodstream), solution);
    }

    private static float GetVenomDamageByVolume(float amount)
    {
        if (amount < 30f)
            return 1f;

        if (amount < 60f)
            return 2f;

        if (amount < 90f)
            return 4f;

        return 8f;
    }
}
