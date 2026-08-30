using Content.Shared.Alert;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Server.Chat.Systems;
using Content.Shared.Emoting;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Aquila.TargetedEmote;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Aquila.TargetedEmote;

public sealed class TargetedEmotesMenuSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const float TimeoutSeconds = 10f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<PlayTargetedEmoteMessage>(OnPlayTargetedEmote);
        SubscribeLocalEvent<EmoteRequestComponent, AcceptEmoteRequestAlertEvent>(OnAcceptRequest);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EmoteRequestComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.ExpiresAt)
            {
                _alerts.ClearAlert(uid, comp.Alert);
                RemCompDeferred<EmoteRequestComponent>(uid);
                continue;
            }

            if (!Exists(comp.Requester))
            {
                _alerts.ClearAlert(uid, comp.Alert);
                RemCompDeferred<EmoteRequestComponent>(uid);
                continue;
            }

            var targetXform = Transform(uid);
            var requesterXform = Transform(comp.Requester);

            if (!_prototypeManager.TryIndex<TargetedEmotePrototype>(comp.EmoteId, out var proto))
                continue;

            var distance = (targetXform.WorldPosition - requesterXform.WorldPosition).Length();
            if (distance > proto.Range)
            {
                _alerts.ClearAlert(uid, comp.Alert);
                RemCompDeferred<EmoteRequestComponent>(uid);
            }
        }
    }

    private void OnPlayTargetedEmote(PlayTargetedEmoteMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        if (!_prototypeManager.TryIndex<TargetedEmotePrototype>(msg.ProtoId, out var proto))
            return;

        var target = GetEntity(msg.Target);
        if (!Exists(target) || target == player.Value)
            return;

        if (!IsInRange(player.Value, target, proto.Range))
            return;

        if (proto.RequiresConsent)
        {
            SendRequest(player.Value, target, proto);
            return;
        }

        PlayEmote(player.Value, target, proto);
    }

    private bool IsInRange(EntityUid a, EntityUid b, float range)
    {
        var posA = Transform(a).WorldPosition;
        var posB = Transform(b).WorldPosition;
        return (posA - posB).Length() <= range;
    }

    private void SendRequest(EntityUid requester, EntityUid target, TargetedEmotePrototype proto)
    {
        if (!string.IsNullOrEmpty(proto.RequestMessage))
            _chat.TrySendInGameICMessage(
                requester,
                Loc.GetString(proto.RequestMessage,
                    ("target", Identity.Entity(target, EntityManager)),
                    ("user", Identity.Entity(requester, EntityManager))),
                InGameICChatType.Emote,
                hideChat: true
            );

        var comp = EnsureComp<EmoteRequestComponent>(target);
        comp.Requester = requester;
        comp.EmoteId = proto.ID;
        comp.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(TimeoutSeconds);

        EnsureComp<AlertsComponent>(target);
        _alerts.ShowAlert(target, comp.Alert,
            cooldown: (_timing.CurTime, _timing.CurTime + TimeSpan.FromSeconds(TimeoutSeconds)),
            showCooldown: true);
        _popup.PopupEntity(
            Loc.GetString("emote-request-popup",
                ("user", Identity.Entity(requester, EntityManager)),
                ("emote", Loc.GetString(proto.Name))),
            target,
            target,
            PopupType.Small
        );
    }

    private void OnAcceptRequest(Entity<EmoteRequestComponent> ent, ref AcceptEmoteRequestAlertEvent args)
    {
        if (!_prototypeManager.TryIndex<TargetedEmotePrototype>(ent.Comp.EmoteId, out var proto))
            return;

        if (!IsInRange(ent.Comp.Requester, ent.Owner, proto.Range))
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
            RemComp<EmoteRequestComponent>(ent.Owner);
            args.Handled = true;
            return;
        }

        PlayEmote(ent.Comp.Requester, ent.Owner, proto);

        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
        RemComp<EmoteRequestComponent>(ent.Owner);

        args.Handled = true;
    }

    private void PlayEmote(EntityUid player, EntityUid target, TargetedEmotePrototype proto)
    {
        if (!string.IsNullOrEmpty(proto.ChatMessage))
            _chat.TrySendInGameICMessage(
                player,
                Loc.GetString(proto.ChatMessage,
                    ("target", Identity.Entity(target, EntityManager)),
                    ("user", Identity.Entity(player, EntityManager))),
                InGameICChatType.Emote,
                hideChat: true
            );

        if (!string.IsNullOrEmpty(proto.TargetedChatMessage))
            _chat.TrySendInGameICMessage(
                target,
                Loc.GetString(proto.TargetedChatMessage,
                    ("user", Identity.Entity(player, EntityManager)),
                    ("target", Identity.Entity(target, EntityManager))),
                InGameICChatType.Emote,
                hideChat: true
            );

        if (proto.Sound != null)
            _audio.PlayPvs(proto.Sound, player);
    }
}
