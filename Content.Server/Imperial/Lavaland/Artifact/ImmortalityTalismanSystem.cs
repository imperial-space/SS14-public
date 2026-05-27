using Content.Server.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Artifact;

public sealed class ImmortalityTalismanSystem : EntitySystem
{
    [Dependency] private readonly GodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier ActivateSound =
        new SoundPathSpecifier("/Audio/Effects/holy.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImmortalityTalismanComponent, UseInHandEvent>(OnUseInHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImmortalityTalismanComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive)
                continue;

            if (now < comp.GodmodeEndTime)
                continue;

            // снять неуязвимость
            comp.IsActive = false;
            if (comp.Holder != null && !TerminatingOrDeleted(comp.Holder.Value))
                _godmode.DisableGodmode(comp.Holder.Value);
            comp.Holder = null;
        }
    }

    private void OnUseInHand(Entity<ImmortalityTalismanComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var now = _timing.CurTime;

        if (now < ent.Comp.CooldownEndTime)
        {
            var remaining = (ent.Comp.CooldownEndTime - now).TotalSeconds;
            _popup.PopupClient($"Перезарядка: {remaining:F0} сек.", args.User, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        _godmode.EnableGodmode(args.User);
        _audio.PlayPvs(ActivateSound, args.User);
        _popup.PopupClient("Талисман активирован! Ты неуязвим на 8 секунд.", args.User, args.User, PopupType.Large);

        ent.Comp.IsActive = true;
        ent.Comp.Holder = args.User;
        ent.Comp.GodmodeEndTime = now + TimeSpan.FromSeconds(ent.Comp.GodmodeDurationSeconds);
        ent.Comp.CooldownEndTime = now + TimeSpan.FromSeconds(ent.Comp.CooldownSeconds);

        args.Handled = true;
    }

}
