using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Friends;

public sealed partial class FriendsSystem
{
    [Dependency] private readonly IChatManager _chatMan = default!;

    private void InitializeRelations()
    {
        SubscribeLocalEvent<FriendsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<FactionDataContainerComponent, MapInitEvent>(OnFactionDataContainerInit);
        SubscribeNetworkEvent<OfferFactionRelationsEvent>(OnOfferRelations);
        SubscribeNetworkEvent<AcceptFactionRelationsEvent>(OnAcceptRelations);
        SubscribeNetworkEvent<DispatchWarEvent>(OnDispatchWar);
    }

    private void OnGetAltVerbs(EntityUid uid, FriendsComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (comp.MenuAccess != FactionMenuAccess.Full)
            return;

        if (!TryComp<FriendsComponent>(args.User, out var friends) || friends.MenuAccess != FactionMenuAccess.Full)
            return;

        if (friends.Faction == comp.Faction)
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

    private void OnOfferRelations(OfferFactionRelationsEvent ev)
    {
        var openEv = new OpenAcceptFactionRelationsEvent(ev.UserFaction, ev.TargetFaction, ev.Relation);
        RaiseNetworkEvent(openEv, GetEntity(ev.Target));
    }

    private void OnAcceptRelations(AcceptFactionRelationsEvent ev)
    {
        if (!TryGetFactionDataContainer(out var cont))
            return;

        ref var relations = ref cont.Value.Comp.Relations;
        relations[ev.UserFaction][ev.TargetFaction] = ev.Relation;
        relations[ev.TargetFaction][ev.UserFaction] = ev.Relation;
        Dirty(cont.Value);

        var userFaction = Proto.Index(ev.UserFaction);
        var targetFaction = Proto.Index(ev.TargetFaction);

        var userMembers = cont.Value.Comp.CachedMembers.GetOrNew(ev.UserFaction);
        var targetMembers = cont.Value.Comp.CachedMembers.GetOrNew(ev.TargetFaction);

        foreach (var item in userMembers.Union(targetMembers))
        {
            if (!GetFactionMemberById(item.Key, out var target) || !_sharedPlayerManager.TryGetSessionByEntity(target.Value, out var session))
                continue;

            var announcement = $"Отношения вашей фракции с {(item.Value.Faction == ev.UserFaction ? targetFaction.Name : userFaction.Name)} изменены на {Proto.Index(ev.Relation).Name}";
            _chatMan.ChatMessageToOne(Shared.Chat.ChatChannel.Radio, announcement, announcement, EntityUid.Invalid, false, session.Channel, Proto.Index(ev.Relation).Color);
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/faction_group_assigned.ogg"), session);
        }
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
