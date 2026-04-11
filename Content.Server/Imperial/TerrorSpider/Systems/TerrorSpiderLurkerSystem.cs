using Content.Server.Actions;
using Content.Server.Imperial.TerrorSpider.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.TerrorSpider.Events;
using Content.Shared.Speech.Muting;
using Content.Shared.Spider;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.TerrorSpider.Systems;

public sealed class TerrorSpiderLurkerSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderLurkerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderLurkerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderLurkerComponent, TerrorSpiderLurkerStealthActionEvent>(OnStealthAction);

        SubscribeLocalEvent<TerrorSpiderLurkerWebAreaComponent, StartCollideEvent>(OnWebStartCollide);
        SubscribeLocalEvent<TerrorSpiderLurkerWebAreaComponent, EndCollideEvent>(OnWebEndCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TerrorSpiderLurkerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.ActionStealthActive || now < comp.ActionStealthEndTime)
                continue;

            comp.ActionStealthActive = false;
            comp.ActionStealthEndTime = TimeSpan.Zero;
            ApplyCurrentState(uid, comp);
            Dirty(uid, comp);
        }
    }

    private void OnMapInit(EntityUid uid, TerrorSpiderLurkerComponent comp, MapInitEvent args)
    {
        _actions.AddAction(uid, ref comp.StealthActionEntity, comp.StealthAction);
        comp.WebContacts = 0;
        comp.ActionStealthActive = false;
        comp.ActionStealthEndTime = TimeSpan.Zero;
        ApplyCurrentState(uid, comp);
    }

    private void OnShutdown(EntityUid uid, TerrorSpiderLurkerComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.StealthActionEntity);
    }

    private void OnStealthAction(Entity<TerrorSpiderLurkerComponent> ent, ref TerrorSpiderLurkerStealthActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.ActionStealthActive = true;
        ent.Comp.ActionStealthEndTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.StealthDuration);
        ApplyCurrentState(ent.Owner, ent.Comp);
        Dirty(ent);
        args.Handled = true;
    }

    private void OnWebStartCollide(Entity<TerrorSpiderLurkerWebAreaComponent> ent, ref StartCollideEvent args)
    {
        if (TryComp<TerrorSpiderLurkerComponent>(args.OtherEntity, out var lurker))
        {
            lurker.WebContacts++;
            ApplyCurrentState(args.OtherEntity, lurker);
            Dirty(args.OtherEntity, lurker);
            return;
        }

        if (HasComp<IgnoreSpiderWebComponent>(args.OtherEntity))
            return;

        _stamina.TakeStaminaDamage(args.OtherEntity, ent.Comp.StaminaDamage, source: ent.Owner);

        if (!TryComp<StatusEffectsComponent>(args.OtherEntity, out var statusEffects))
            return;

        _statusEffects.TryAddStatusEffect<MutedComponent>(
            args.OtherEntity,
            "Muted",
            TimeSpan.FromSeconds(ent.Comp.MuteDuration),
            true,
            statusEffects);
    }

    private void OnWebEndCollide(Entity<TerrorSpiderLurkerWebAreaComponent> ent, ref EndCollideEvent args)
    {
        if (!TryComp<TerrorSpiderLurkerComponent>(args.OtherEntity, out var lurker))
            return;

        lurker.WebContacts = Math.Max(0, lurker.WebContacts - 1);
        ApplyCurrentState(args.OtherEntity, lurker);
        Dirty(args.OtherEntity, lurker);
    }

    private void ApplyCurrentState(EntityUid uid, TerrorSpiderLurkerComponent comp)
    {
        var onLurkerWeb = comp.WebContacts > 0;
        var shouldBeStealthed = comp.ActionStealthActive || onLurkerWeb;

        if (TryComp<MeleeWeaponComponent>(uid, out var melee))
        {
            melee.Damage.DamageDict["Piercing"] = FixedPoint2.New(onLurkerWeb ? comp.WebBuffMeleeDamage : comp.BaseMeleeDamage);
            Dirty(uid, melee);
        }

        if (onLurkerWeb)
        {
            var staminaOnHit = EnsureComp<StaminaDamageOnHitComponent>(uid);
            staminaOnHit.Damage = comp.WebBuffStaminaDamage;
            Dirty(uid, staminaOnHit);
        }
        else if (HasComp<StaminaDamageOnHitComponent>(uid))
        {
            RemComp<StaminaDamageOnHitComponent>(uid);
        }

        if (shouldBeStealthed)
        {
            var stealth = EnsureComp<StealthComponent>(uid);
            _stealth.SetEnabled(uid, true, stealth);
            _stealth.SetVisibility(uid, comp.MinStealthVisibility, stealth);
            Dirty(uid, stealth);
        }
        else if (HasComp<StealthComponent>(uid))
        {
            RemComp<StealthComponent>(uid);
        }
    }
}
