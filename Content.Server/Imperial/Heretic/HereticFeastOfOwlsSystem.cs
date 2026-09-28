using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticFeastOfOwlsSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem      _heretic     = default!;
    [Dependency] private readonly PopupSystem        _popup       = default!;
    [Dependency] private readonly SharedAudioSystem  _audio       = default!;
    [Dependency] private readonly SharedStunSystem   _stun        = default!;
    [Dependency] private readonly IChatManager       _chatManager = default!;

    private static readonly SoundPathSpecifier FeastSound =
        new("/Audio/Imperial/heretic/sound_effects_curse.ogg");

    public void OnFeastCreated(EntityUid spawned, HereticFeastOwlsResultComponent comp, EntityUid caster)
    {
        if (TryComp<HereticComponent>(caster, out var heretic))
        {
            if (heretic.FeastOfOwlsUsed)
            {
                _popup.PopupEntity(Loc.GetString("heretic-feast-of-owls-already-used"), caster, caster, PopupType.MediumCaution);
                QueueDel(spawned);
                return;
            }

            _heretic.SetFeastOfOwlsUsed(caster, heretic);

            _heretic.AddKnowledgePoints(caster, heretic, 5);
            _heretic.SetAscensionTriggered(caster, heretic);
            _audio.PlayPvs(FeastSound, caster);
            _popup.PopupEntity(Loc.GetString("heretic-feast-of-owls-complete"), caster, caster, PopupType.LargeCaution);
            if (TryComp<ActorComponent>(caster, out var casterActor))
            {
                var rawMsg = Loc.GetString("heretic-feast-of-owls-whisper");
                var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", rawMsg));
                _chatManager.ChatMessageToOne(ChatChannel.Server, rawMsg, wrapped, default, false, casterActor.PlayerSession.Channel);
            }
            _stun.TryKnockdown(caster, TimeSpan.FromSeconds(10), true);
        }
        QueueDel(spawned);
    }
}
