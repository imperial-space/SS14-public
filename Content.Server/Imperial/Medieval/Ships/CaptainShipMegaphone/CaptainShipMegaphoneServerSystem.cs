using Content.Server.Chat.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Interaction.Events;
using Content.Shared.Timing;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;

public sealed class CaptainShipMegaphoneServerSystem : EntitySystem
{
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CaptainShipMegaphoneComponent, UseInHandEvent>(OnUseInHand);
        SubscribeNetworkEvent<CaptainShipMegaphoneSelectedCommandMessage>(OnCommandSelected);
    }

    private void OnUseInHand(EntityUid uid, CaptainShipMegaphoneComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        RaiseNetworkEvent(new CaptainShipMegaphoneOpenMessage(GetNetEntity(uid)), actor.PlayerSession);

        args.Handled = true;
    }

    private void OnCommandSelected(CaptainShipMegaphoneSelectedCommandMessage msg, EntitySessionEventArgs args)
    {
        var playerSession = args.SenderSession;

        if (playerSession.AttachedEntity is not { } playerEntity)
            return;

        var megaphoneUid = GetEntity(msg.Megaphone);
        var text = Loc.GetString(msg.Text);

        if (!TryComp<UseDelayComponent>(megaphoneUid, out var useDelayComp) || _useDelay.IsDelayed((megaphoneUid, useDelayComp)))
            return;

        if (!Exists(megaphoneUid) && !_hands.IsHolding(playerEntity, megaphoneUid))
            return;

        if (Transform(playerEntity).GridUid is not { } currentGridUid)
            return;

        if (!TryComp<ShipDrowningComponent>(currentGridUid, out _))
            return;

        var filter = Filter.BroadcastGrid(currentGridUid);
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Voice/Talk/speak_2_exclaim.ogg"), filter, false);

        _chat.DispatchFilteredAnnouncement(filter, text, playerEntity, Name(playerEntity), playSound: false, colorOverride: Color.LightGoldenrodYellow);

        _useDelay.TryResetDelay((megaphoneUid, useDelayComp));
    }
}
