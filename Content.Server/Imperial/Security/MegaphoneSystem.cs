using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// obj/item/megaphone: речь с мегафоном в активной руке звучит крупнее (SPAN_COMMAND),
/// раз в 5 секунд. Емаг делает голос огромным.
/// </summary>
public sealed class MegaphoneSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>Тик последнего срабатывания: одна реплика вызывает событие и для речи, и для рации.</summary>
    private readonly Dictionary<EntityUid, GameTick> _lastTick = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MegaphoneComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<MegaphoneComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<MegaphoneComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<MegaphoneHolderComponent, TransformSpeakerNameEvent>(OnSpeak);
        SubscribeLocalEvent<MegaphoneHolderComponent, ComponentShutdown>(OnHolderShutdown);
    }

    private void OnEquipped(Entity<MegaphoneComponent> ent, ref GotEquippedHandEvent args)
    {
        EnsureComp<MegaphoneHolderComponent>(args.User);
    }

    private void OnUnequipped(Entity<MegaphoneComponent> ent, ref GotUnequippedHandEvent args)
    {
        foreach (var held in _hands.EnumerateHeld(args.User))
        {
            if (held != ent.Owner && HasComp<MegaphoneComponent>(held))
                return;
        }

        RemCompDeferred<MegaphoneHolderComponent>(args.User);
    }

    private void OnHolderShutdown(Entity<MegaphoneHolderComponent> ent, ref ComponentShutdown args)
    {
        _lastTick.Remove(ent);
    }

    /// <summary>handle_speech().</summary>
    private void OnSpeak(Entity<MegaphoneHolderComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (_hands.GetActiveItem(ent.Owner) is not { } item || !TryComp<MegaphoneComponent>(item, out var megaphone))
            return;

        var tick = _timing.CurTick;
        if (_lastTick.TryGetValue(ent, out var last) && last == tick)
        {
            args.SpeechVerb = megaphone.Emagged ? megaphone.EmaggedSpeechVerb : megaphone.SpeechVerb;
            return;
        }

        if (_timing.CurTime < megaphone.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("megaphone-recharge", ("item", item)), ent, ent, PopupType.SmallCaution);
            return;
        }

        _lastTick[ent] = tick;
        megaphone.NextUse = _timing.CurTime + megaphone.Cooldown;
        args.SpeechVerb = megaphone.Emagged ? megaphone.EmaggedSpeechVerb : megaphone.SpeechVerb;
        _audio.PlayPvs(megaphone.Sound, ent);
    }

    private void OnEmagged(Entity<MegaphoneComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || ent.Comp.Emagged)
            return;

        ent.Comp.Emagged = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("megaphone-emagged"), ent, args.UserUid);
    }
}
