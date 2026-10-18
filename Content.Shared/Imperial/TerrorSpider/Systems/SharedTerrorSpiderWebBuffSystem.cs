using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.TerrorSpider.Systems;

public abstract class SharedTerrorSpiderWebBuffSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderWebBuffReceiverComponent, MapInitEvent>(OnReceiverMapInit);
        SubscribeLocalEvent<TerrorSpiderWebBuffReceiverComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMove);
        SubscribeLocalEvent<TerrorSpiderWebBuffAreaComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<TerrorSpiderWebBuffAreaComponent, EndCollideEvent>(OnEndCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TerrorSpiderWebBuffReceiverComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.WebContacts <= 0)
                continue;

            if (comp.NextRegenTick == TimeSpan.Zero)
                comp.NextRegenTick = now + TimeSpan.FromSeconds(comp.RegenInterval);

            while (now >= comp.NextRegenTick)
            {
                var regenDamage = new DamageSpecifier();
                regenDamage.DamageDict["Brute"] = -comp.RegenPerTick;
                _damageable.TryChangeDamage(uid, regenDamage, ignoreResistances: true, interruptsDoAfters: false);
                comp.NextRegenTick += TimeSpan.FromSeconds(comp.RegenInterval);
            }
        }
    }

    private void OnReceiverMapInit(EntityUid uid, TerrorSpiderWebBuffReceiverComponent comp, MapInitEvent args)
    {
        comp.WebContacts = 0;
        comp.NextRegenTick = TimeSpan.Zero;

        // Dark vision for all terror spiders
        if (TryComp<EyeComponent>(uid, out var eye))
        {
            _eye.SetDrawLight((uid, eye), false);
        }
    }

    private void OnRefreshMove(Entity<TerrorSpiderWebBuffReceiverComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.WebContacts <= 0)
            return;

        args.ModifySpeed(ent.Comp.SpeedMultiplier);
    }

    private void OnStartCollide(Entity<TerrorSpiderWebBuffAreaComponent> ent, ref StartCollideEvent args)
    {
        if (!TryComp<TerrorSpiderWebBuffReceiverComponent>(args.OtherEntity, out var receiver))
            return;

        receiver.WebContacts++;
        _movement.RefreshMovementSpeedModifiers(args.OtherEntity);
        Dirty(args.OtherEntity, receiver);
    }

    private void OnEndCollide(Entity<TerrorSpiderWebBuffAreaComponent> ent, ref EndCollideEvent args)
    {
        if (!TryComp<TerrorSpiderWebBuffReceiverComponent>(args.OtherEntity, out var receiver))
            return;

        receiver.WebContacts = Math.Max(0, receiver.WebContacts - 1);
        if (receiver.WebContacts <= 0)
            receiver.NextRegenTick = TimeSpan.Zero;

        _movement.RefreshMovementSpeedModifiers(args.OtherEntity);
        Dirty(args.OtherEntity, receiver);
    }
}
