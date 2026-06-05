using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Asclepius;

public sealed class AsclepiusRodSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AsclepiusRodComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AsclepiusRodComponent, UseInHandEvent>(OnUseInHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AsclepiusRodComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.OathTaken)
                continue;

            if (now < comp.NextHealTime)
                continue;

            if (!TryGetHolder(uid, out var holder))
                continue;

            HealAround(holder, comp);
            comp.NextHealTime = now + TimeSpan.FromSeconds(MathF.Max(0.1f, comp.HealIntervalSeconds));
        }
    }

    private void OnMapInit(Entity<AsclepiusRodComponent> ent, ref MapInitEvent args)
    {
        SetRodVisualState(ent, ent.Comp.OathTaken);
    }

    private void OnUseInHand(Entity<AsclepiusRodComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.OathTaken)
        {
            _popup.PopupClient("Клятва уже принесена.", args.User, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        ent.Comp.OathTaken = true;
        ent.Comp.NextHealTime = _timing.CurTime;

        EnsureComp<PacifiedComponent>(args.User);
        EnsureComp<UnremoveableComponent>(ent.Owner);
        SetRodVisualState(ent, true);

        _popup.PopupClient("Клятва Асклепия принята. Теперь ты не можешь вредить живым существам.", args.User, args.User, PopupType.Large);
        args.Handled = true;
    }

    private void SetRodVisualState(Entity<AsclepiusRodComponent> ent, bool active)
    {
        var heldPrefix = active ? ent.Comp.ActiveHeldPrefix : ent.Comp.DormantHeldPrefix;

        if (TryComp(ent, out ItemComponent? itemComp))
        {
            _item.SetHeldPrefix(ent, heldPrefix, component: itemComp);
        }
    }

    private bool TryGetHolder(EntityUid rod, out EntityUid holder)
    {
        holder = default;
        var parent = Transform(rod).ParentUid;
        if (!parent.IsValid() || !TryComp<HandsComponent>(parent, out var hands))
            return false;

        if (!_hands.IsHolding((parent, hands), rod))
            return false;

        holder = parent;
        return true;
    }

    private void HealAround(EntityUid holder, AsclepiusRodComponent comp)
    {
        var holderXform = Transform(holder);

        if (!TryComp<MobStateComponent>(holder, out var holderMob) || holderMob.CurrentState == MobState.Dead)
            return;

        var holderPos = _transform.GetWorldPosition(holder);
        var rangeSquared = comp.HealRange * comp.HealRange;
        var mapId = holderXform.MapID;

        var query = EntityQueryEnumerator<MobStateComponent, DamageableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var damageable, out var xform))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            if (xform.MapID != mapId)
                continue;

            var distSquared = (_transform.GetWorldPosition(uid) - holderPos).LengthSquared();
            if (distSquared > rangeSquared)
                continue;

            _damageable.ChangeDamage((uid, damageable), comp.HealPerTick, true, false);
        }
    }
}
