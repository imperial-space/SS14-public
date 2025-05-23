using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Robust.Server.Audio;


namespace Content.Server.Imperial.Medieval.Magic.Triggers;


public sealed partial class StaminaDamageOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaminaDamageOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(EntityUid uid, StaminaDamageOnTriggerComponent component, TriggerEvent args)
    {
        if (!HasComp<StaminaComponent>(args.Triggered))
            return;

        var ev = new StaminaDamageOnHitAttemptEvent();
        RaiseLocalEvent(uid, ref ev);

        if (ev.Cancelled) return;

        _staminaSystem.TakeStaminaDamage(args.Triggered, component.Damage, sound: component.Sound, source: uid);
    }
}
