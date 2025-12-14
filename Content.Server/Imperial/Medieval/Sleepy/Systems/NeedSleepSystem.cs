using Content.Server.NeedSleep.Components;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Alert;
using Content.Server.Stunnable;
using Content.Shared.StatusEffect;
using Content.Server.Chat.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Server.Imperial.Medieval.NeedSleep;

namespace Content.Server.NeedSleep
{
    public sealed partial class NeedSleepSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly StunSystem _stun = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<NeedSleepComponent, ExaminedEvent>(OnExamine);

            SubscribeLocalEvent<NeedSleepClearComponent, ComponentStartup>(OnClearStart);
            SubscribeLocalEvent<NeedNoSleepComponent, ComponentStartup>(OnInsomniaStart);

            //SubscribeLocalEvent<NeedSleepComponent, WakeActionEvent>(OnWakeAction);
        }

        //private void OnWakeAction(EntityUid uid, NeedSleepComponent component, ref WakeActionEvent args)
        //{
        //    if (component.SleepLevel > 80f)
        //        EnsureComp<SleepingComponent>(component.Owner);
        //}

        private void OnClearStart(EntityUid uid, NeedSleepClearComponent component, ComponentStartup args)
        {
            if (TryComp<NeedSleepComponent>(uid, out var need))
                need.SleepLevelPerUpdate *= 0.7f;
        }

        private void OnInsomniaStart(EntityUid uid, NeedNoSleepComponent component, ComponentStartup args)
        {
            if (TryComp<NeedSleepComponent>(uid, out var need))
                need.SleepLevelPerUpdate *= 1.4f;
        }

        private void OnExamine(EntityUid uid, NeedSleepComponent component, ExaminedEvent args)
        {
            if (component.SleepLevel > 80f)
                args.PushMarkup("[color=cyan]Сонные глаза[/color]");
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<NeedSleepComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (_timing.CurTime < comp.NextUpdate)
                    continue;

                comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(comp.UpdateInterval);

                if (!comp.Enabled)
                    continue;

                var ev = new GetSleepLevelModifiersEvent(HasComp<SleepingComponent>(uid));
                RaiseLocalEvent(uid, ref ev);

                comp.SleepLevel += (HasComp<SleepingComponent>(uid) ? -comp.SleepRegenPerUpdate : comp.SleepLevelPerUpdate) * ev.Modifier;

                _alerts.ShowAlert(uid, comp.TiredAlert, (short)Math.Clamp(Math.Round(comp.SleepLevel / comp.MaxSleepLevel * 4.1f), 0, 4));

                if (HasComp<SleepingComponent>(uid))
                    continue;

                var emote = _random.Pick(comp.Emotes);
                if (_random.Prob(comp.SleepLevel / 450f) && comp.SleepLevel > 65 && !HasComp<SleepingComponent>(uid))
                    _chatSystem.TryEmoteWithChat(uid, emote, ChatTransmitRange.Normal);

                if (comp.SleepLevel > 96.5f)
                {
                    _popup.PopupEntity("Вам нужно выспаться", uid, uid, PopupType.LargeCaution);
                    //if (TryComp<StatusEffectsComponent>(uid, out var status))
                    //    _stun.TrySlowdown(uid, TimeSpan.FromSeconds(9.99f), true, 0.85f, 0.85f, status); // need to fix
                }

                if (comp.SleepLevel >= comp.MaxSleepLevel)
                    EnsureComp<SleepingComponent>(uid);
            }
        }
    }
}
