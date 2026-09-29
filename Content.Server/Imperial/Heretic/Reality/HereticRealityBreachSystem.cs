using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Body;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Reality;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic.Reality;

public sealed class HereticRealityBreachSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem          _heretic    = default!;
    [Dependency] private readonly IChatManager           _chatManager = default!;
    [Dependency] private readonly IRobustRandom          _random      = default!;
    [Dependency] private readonly IGameTiming            _timing      = default!;
    [Dependency] private readonly SharedContainerSystem  _container   = default!;

    private static readonly TimeSpan GrayScaleDuration = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ExamineWindow     = TimeSpan.FromSeconds(60);
    private const int ExamineTriggerCount = 4;
    private static readonly string[] HereticExamineMessages =
    {
        "heretic-breach-heretic-examine-1",
        "heretic-breach-heretic-examine-2",
        "heretic-breach-heretic-examine-3",
        "heretic-breach-heretic-examine-4",
        "heretic-breach-heretic-examine-5",
        "heretic-breach-heretic-examine-6",
    };

    private static readonly TimeSpan TouchWindow = TimeSpan.FromSeconds(60);
    private const int TouchTriggerCount = 4;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRealityBreachComponent, ExaminedEvent>(OnBreachExamined);
        SubscribeLocalEvent<HereticRealityBreachComponent, InteractHandEvent>(OnBreachInteract);
        SubscribeLocalEvent<HereticRealityBreachComponent, ActivateInWorldEvent>(OnBreachActivate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BreachGrayScaleComponent>();
        while (query.MoveNext(out var uid, out var grayScale))
        {
            if (now >= grayScale.EndTime)
                RemCompDeferred<BreachGrayScaleComponent>(uid);
        }
    }

    private void OnBreachExamined(EntityUid uid, HereticRealityBreachComponent _, ExaminedEvent args)
    {
        var examiner = args.Examiner;

        if (HasComp<HereticComponent>(examiner))
        {
            var msgKey = _random.Pick(HereticExamineMessages);
            _heretic.SendHereticMessage(examiner, Loc.GetString(msgKey));
        }
        else
        {
            SendMessageToPlayer(examiner, Loc.GetString("heretic-breach-nonheretic-examine"));
            TryTriggerGrayScale(examiner);
        }
    }

    private void OnBreachInteract(EntityUid uid, HereticRealityBreachComponent _, InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        HandleBreachInteract(uid, args.User);
    }

    private void OnBreachActivate(EntityUid uid, HereticRealityBreachComponent _, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        HandleBreachInteract(uid, args.User);
    }

    private void HandleBreachInteract(EntityUid uid, EntityUid player)
    {
        if (HasComp<HereticComponent>(player))
        {
            _heretic.SendHereticMessage(player, Loc.GetString("heretic-breach-heretic-interact"));
            return;
        }

        var now = _timing.CurTime;

        var interaction = EnsureComp<HereticBreachInteractionComponent>(player);
        if (now - interaction.TouchWindowStart > TouchWindow)
        {
            interaction.TouchCount = 0;
            interaction.TouchWindowStart = now;
        }

        interaction.TouchCount++;
        if (interaction.TouchCount < TouchTriggerCount)
        {
            SendMessageToPlayer(player, Loc.GetString("heretic-breach-nonheretic-interact"));
            return;
        }

        interaction.TouchCount = 0;
        interaction.TouchWindowStart = now;
        TryAmputateActiveHand(player);
        SendMessageToPlayer(player, Loc.GetString("heretic-breach-nonheretic-hand-ripped"));
    }

    private void TryAmputateActiveHand(EntityUid player)
    {
        if (!TryComp<HandsComponent>(player, out var handsComp))
            return;

        var activeHandId = handsComp.ActiveHandId;

        if (!_container.TryGetContainer(player, BodyComponent.ContainerID, out var organs))
            return;

        foreach (var organ in organs.ContainedEntities.ToArray())
        {
            if (!TryComp<HandOrganComponent>(organ, out var handOrgan))
                continue;
            if (activeHandId != null && handOrgan.HandID != activeHandId)
                continue;

            _container.Remove(organ, organs, force: true);
            return;
        }
    }

    private void TryTriggerGrayScale(EntityUid player)
    {
        var now = _timing.CurTime;

        var interaction = EnsureComp<HereticBreachInteractionComponent>(player);
        if (now - interaction.ExamineWindowStart > ExamineWindow)
        {
            interaction.ExamineCount = 0;
            interaction.ExamineWindowStart = now;
        }

        interaction.ExamineCount++;
        if (interaction.ExamineCount < ExamineTriggerCount)
            return;

        interaction.ExamineCount = 0;
        interaction.ExamineWindowStart = now;

        if (HasComp<BreachGrayScaleComponent>(player))
            return;

        var grayScale = AddComp<BreachGrayScaleComponent>(player);
        grayScale.EndTime = now + GrayScaleDuration;
    }

    private void SendMessageToPlayer(EntityUid uid, string message)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, actor.PlayerSession.Channel);
    }
}
