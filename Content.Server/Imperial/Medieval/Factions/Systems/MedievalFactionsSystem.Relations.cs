using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Server.Imperial.Medieval.Factions.Components;
using Content.Shared.Paper;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class MedievalFactionsSystem
{
    [Dependency] private readonly IChatManager _chatMan = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    private void InitializeRelations()
    {
        SubscribeLocalEvent<MedievalFactionMemberComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<FactionDataContainerComponent, MapInitEvent>(OnFactionDataContainerInit);
        SubscribeLocalEvent<MedievalRelationRequestPaperComponent, GetVerbsEvent<AlternativeVerb>>(OnGetRequestVerbs);
        SubscribeNetworkEvent<OfferFactionRelationsEvent>(OnOfferRelations);
        SubscribeNetworkEvent<AcceptFactionRelationsEvent>(OnAcceptRelations);
        SubscribeNetworkEvent<SetFactionRelationsByRequestEvent>(OnSetRelationsByRequest);
        SubscribeNetworkEvent<CreateFactionRelationsRequestEvent>(OnCreateRequest);
        SubscribeNetworkEvent<DispatchWarEvent>(OnDispatchWar);
    }

    private void OnGetAltVerbs(EntityUid uid, MedievalFactionMemberComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (comp.MenuAccess != FactionMenuAccess.Full)
            return;

        if (!TryComp<MedievalFactionMemberComponent>(args.User, out var friends) || friends.MenuAccess != FactionMenuAccess.Full)
            return;

        if (friends.Faction == comp.Faction)
            return;

        if (Proto.Index(friends.Faction).BlockedRelations.Contains(comp.Faction) ||
            Proto.Index(comp.Faction).BlockedRelations.Contains(friends.Faction))
            return;

        AlternativeVerb verb = new()
        {
            Text = "Изменить отношения",
            Act = () =>
            {
                var ev = new OpenOfferFactionRelationsEvent(GetNetEntity(uid), friends.Faction, comp.Faction);
                RaiseNetworkEvent(ev, args.User);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnGetRequestVerbs(EntityUid uid, MedievalRelationRequestPaperComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!TryComp<MedievalFactionMemberComponent>(args.User, out var friends) || friends.MenuAccess != FactionMenuAccess.Full)
            return;

        if (HasComp<MedievalFactionRelationsRequestComponent>(uid))
            return;

        AlternativeVerb verb = new()
        {
            Text = "Сделать запрос на смену отношений",
            Act = () =>
            {
                var ev = new OpenFactionRelationsRequestEvent(GetNetEntity(uid), friends.Faction);
                RaiseNetworkEvent(ev, args.User);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnOfferRelations(OfferFactionRelationsEvent ev)
    {
        var openEv = new OpenAcceptFactionRelationsEvent(ev.UserFaction, ev.TargetFaction, ev.Relation);
        RaiseNetworkEvent(openEv, GetEntity(ev.Target));
    }

    private void OnAcceptRelations(AcceptFactionRelationsEvent ev)
    {
        SetRelations(ev.UserFaction, ev.TargetFaction, ev.Relation);
    }

    private void OnSetRelationsByRequest(SetFactionRelationsByRequestEvent ev)
    {
        if (ev.Decline)
        {
            RemComp<MedievalFactionRelationsRequestComponent>(GetEntity(ev.Target));
            return;
        }

        if (!TryComp<MedievalFactionRelationsRequestComponent>(GetEntity(ev.Target), out var request))
            return;

        SetRelations(request.From, request.To, request.Relation);
        RemComp<MedievalFactionRelationsRequestComponent>(GetEntity(ev.Target));
    }

    private void OnCreateRequest(CreateFactionRelationsRequestEvent ev)
    {
        var target = GetEntity(ev.Target);
        var faction = Proto.Index(ev.UserFaction);

        var coords = Transform(target).Coordinates;
        if (_container.TryGetContainingContainer(target, out var container))
            coords = Transform(container.Owner).Coordinates;

        var env = Spawn(faction.EnvelopeProto, coords);

        var comp = EnsureComp<MedievalFactionRelationsRequestComponent>(env);
        comp.From = ev.UserFaction;
        comp.To = ev.TargetFaction;
        comp.Relation = ev.Relation;
        Dirty(env, comp);

        _paper.SetContent(env, Comp<PaperComponent>(target).Content);

        Comp<PaperComponent>(target).EditingDisabled = true;
        QueueDel(target);

        if (container != null)
            _container.InsertOrDrop(env, container);
    }

    private void OnDispatchWar(DispatchWarEvent ev)
    {
        if (!TryGetFactionDataContainer(out var cont))
            return;

        ref var relations = ref cont.Value.Comp.Relations;
        relations[ev.UserFaction][ev.TargetFaction] = "War";
        relations[ev.TargetFaction][ev.UserFaction] = "War";
        Dirty(cont.Value);

        var userFaction = Proto.Index(ev.UserFaction);
        var targetFaction = Proto.Index(ev.TargetFaction);

        var userMembers = cont.Value.Comp.CachedMembers.GetOrNew(ev.UserFaction);
        var targetMembers = cont.Value.Comp.CachedMembers.GetOrNew(ev.TargetFaction);

        foreach (var item in userMembers.Union(targetMembers))
        {
            if (!GetFactionMemberById(item.Key, out var target) || !_sharedPlayerManager.TryGetSessionByEntity(target.Value, out var session))
                continue;

            var announcement = $"Отношения вашей фракции с {(item.Value.Faction == ev.UserFaction ? targetFaction.Name : userFaction.Name)} изменены на {Proto.Index<FactionRelationsPrototype>("War").Name}";
            _chatMan.ChatMessageToOne(Shared.Chat.ChatChannel.Radio, announcement, announcement, EntityUid.Invalid, false, session.Channel, Proto.Index<FactionRelationsPrototype>("War").Color);
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/faction_group_assigned.ogg"), session);
        }
    }

    private void SetRelations(ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction, ProtoId<FactionRelationsPrototype> relation)
    {
        if (!TryGetFactionDataContainer(out var cont))
            return;

        ref var relations = ref cont.Value.Comp.Relations;
        relations[userFaction][targetFaction] = relation;
        relations[targetFaction][userFaction] = relation;
        Dirty(cont.Value);

        var userFactionProto = Proto.Index(userFaction);
        var targetFactionProto = Proto.Index(targetFaction);
        var userMembers = cont.Value.Comp.CachedMembers.GetOrNew(userFaction);
        var targetMembers = cont.Value.Comp.CachedMembers.GetOrNew(targetFaction);

        foreach (var item in userMembers.Union(targetMembers))
        {
            if (!GetFactionMemberById(item.Key, out var target) || !_sharedPlayerManager.TryGetSessionByEntity(target.Value, out var session))
                continue;

            var announcement = $"Отношения вашей фракции с {(item.Value.Faction == userFaction ? targetFactionProto.Name : userFactionProto.Name)} изменены на {Proto.Index(relation).Name}";
            _chatMan.ChatMessageToOne(Shared.Chat.ChatChannel.Radio, announcement, announcement, EntityUid.Invalid, false, session.Channel, Proto.Index(relation).Color);
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/faction_group_assigned.ogg"), session);
        }
    }

    private void OnFactionDataContainerInit(EntityUid uid, FactionDataContainerComponent comp, MapInitEvent args)
    {
        var factions = Proto.EnumeratePrototypes<MedievalFactionPrototype>();
        foreach (var item in factions)
        {
            foreach (var item2 in factions)
            {
                if (item == item2)
                    continue;

                comp.Relations.TryAdd(item.ID, new());
                comp.Relations[item.ID].Add(item2.ID, item.DefaultRelations.GetValueOrDefault(item2.ID, "Neutral"));
            }
        }

        Dirty(uid, comp);
    }
}
