using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticUnfathomableCurioSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup  = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticUnfathomableCurioComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticUnfathomableCurioComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticCurioShieldComponent, ComponentShutdown>(OnShieldShutdown);
        SubscribeLocalEvent<HereticCurioShieldComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticCurioShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (shield.ShieldActive || shield.LastAbsorbedTime == null)
                continue;

            if (_timing.CurTime - shield.LastAbsorbedTime.Value >= shield.RechargeDelay)
            {
                shield.ShieldActive = true;
                shield.LastAbsorbedTime = null;
                SpawnShieldVisual(uid, shield);
                _popup.PopupEntity(Loc.GetString("heretic-curio-shield-recharged"), uid, uid, PopupType.Small);
                Dirty(uid, shield);
            }
        }
    }

    private void OnEquipped(Entity<HereticUnfathomableCurioComponent> ent, ref ClothingGotEquippedEvent args)
    {
        var shield = EnsureComp<HereticCurioShieldComponent>(args.Wearer);
        if (shield.ShieldActive)
        {
            SpawnShieldVisual(args.Wearer, shield);
            _popup.PopupEntity(Loc.GetString("heretic-curio-equipped"), args.Wearer, args.Wearer, PopupType.Small);
        }
        Dirty(args.Wearer, shield);
    }

    private void OnUnequipped(Entity<HereticUnfathomableCurioComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (TryComp<HereticCurioShieldComponent>(args.Wearer, out var shield))
            RemoveShieldVisual(shield);
        RemComp<HereticCurioShieldComponent>(args.Wearer);
    }

    private void OnShieldShutdown(Entity<HereticCurioShieldComponent> ent, ref ComponentShutdown args)
        => RemoveShieldVisual(ent.Comp);

    private void OnBeforeDamage(EntityUid uid, HereticCurioShieldComponent shield, ref BeforeDamageChangedEvent args)
    {
        if (!shield.ShieldActive)
            return;
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;

        args.Cancelled = true;
        shield.ShieldActive = false;
        shield.LastAbsorbedTime = _timing.CurTime;
        RemoveShieldVisual(shield);
        _popup.PopupEntity(Loc.GetString("heretic-curio-shield-absorbed"), uid, uid, PopupType.MediumCaution);
        Dirty(uid, shield);
    }

    private void SpawnShieldVisual(EntityUid wearer, HereticCurioShieldComponent shield)
    {
        RemoveShieldVisual(shield);
        var visual = Spawn("HereticCurioShieldVisual", Transform(wearer).Coordinates);
        _xform.SetParent(visual, wearer);
        shield.ShieldVisual = visual;
    }

    private void RemoveShieldVisual(HereticCurioShieldComponent shield)
    {
        if (shield.ShieldVisual != null && !Deleted(shield.ShieldVisual.Value))
        {
            QueueDel(shield.ShieldVisual.Value);
            shield.ShieldVisual = null;
        }
    }
}
