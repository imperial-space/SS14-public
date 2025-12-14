using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Actions;
using Content.Server.GameTicking;
using Content.Server.Imperial.Medieval.CombatStance;
using Content.Server.MedievalPasport;
using Content.Server.MedievalPasport.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles.Jobs;
using Content.Server.Station.Components;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.IdentityManagement;
using Robust.Server.Audio;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class MedievalFactionsSystem
{
    [Dependency] private readonly JobSystem _job = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IEntitySystemManager _entity = default!;

    private int _nextId = 1;

    private void InitializeMenu()
    {
        SubscribeLocalEvent<MedievalFactionMemberComponent, StartupFactionDataEvent>(OnFriendsInit);
        SubscribeLocalEvent<MedievalFactionMemberComponent, EntityTerminatingEvent>(OnFriendsTerminating);

        SubscribeNetworkEvent<SetFactionMemberObjectiveMessage>(OnSetObjective);
        SubscribeNetworkEvent<SetFactionMemberGroupMessage>(OnSetGroup);
        SubscribeNetworkEvent<RemoveFactionMemberMessage>(OnMemberRemoved);
        SubscribeNetworkEvent<SetGroupLeaderMessage>(OnSetLeader);
    }

    private void OnFriendsInit(EntityUid uid, MedievalFactionMemberComponent comp, StartupFactionDataEvent args)
    {
        if (!EnsureFactionDataContainer(out var container))
            return;

        comp.MemberID = _nextId;
        _nextId++;

        var data = new FactionMemberData()
        {
            Name = Name(uid),
            Job = args.Job,
            JobPrefix = args.JobPrefix,
            Faction = comp.Faction
        };
        container.Value.Comp.CachedMembers.GetOrNew(comp.Faction).Add(comp.MemberID, data);

        if (comp.Faction != "Voluntary")
            _action.AddAction(uid, ref comp.FactionMenuActionEntity, comp.FactionMenuAction);

        Dirty(uid, comp);
        RefreshFactionMenu(comp.Faction);

        if (!TryComp<IdentityRequiresKnowledgeComponent>(uid, out var selfIdent))
            return;

        foreach (var item in container.Value.Comp.CachedMembers.GetOrNew(comp.Faction))
        {
            if (!GetFactionMemberById(item.Key, out var ent))
                continue;

            if (!TryComp<IdentityRequiresKnowledgeComponent>(ent, out var ident))
                continue;

            ident.KnownIds.Add(selfIdent.Identifier);
            selfIdent.KnownIds.Add(ident.Identifier);
            Dirty(ent.Value, ident);
        }
        if (comp.MenuAccess == FactionMenuAccess.Full)
            _action.AddAction(uid, "FactionRadialPointsMenu");
        Dirty(uid, selfIdent);
    }

    private void OnFriendsTerminating(EntityUid uid, MedievalFactionMemberComponent comp, EntityTerminatingEvent args)
    {
        if (!TryGetFactionMemberData(comp.MemberID, out var data))
            return;

        data.Dead = true;
        RefreshFactionMenu(comp.Faction);
    }

    private void OnSetObjective(SetFactionMemberObjectiveMessage args)
    {
        if (!EnsureFactionDataContainer(out var ent))
            return;
        var dict = ent.Value.Comp.Objectives.GetOrNew(args.Faction);
        if (dict.TryGetValue(args.Group, out _))
            dict[args.Group] = args.Objective;
        else
            dict.Add(args.Group, args.Objective);

        foreach (var item in ent.Value.Comp.CachedMembers.GetOrNew(args.Faction).Where(x => x.Value.Group == args.Group))
        {
            if (GetFactionMemberById(item.Key, out var uid))
            {
                _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/faction_group_assigned.ogg"), uid.Value);
                _popup.PopupEntity("Вам была назначена новая задача.", uid.Value, uid.Value, Shared.Popups.PopupType.Medium);
            }
        }

        RefreshFactionMenu(args.Faction);
    }

    private void OnSetGroup(SetFactionMemberGroupMessage args)
    {
        if (!GetFactionMemberById(args.Ent, out var uid))
            return;
        if (!uid.Value.IsValid())
            return;
        if (!TryComp<MedievalFactionMemberComponent>(uid, out var comp))
            return;
        if (!TryGetFactionMemberData(args.Ent, out var data))
            return;
        var newgroup = args.Group;
        _entity.GetEntitySystem<CombatStancePointTestSystem>().GroupChanged(uid.Value, comp.Faction, newgroup, data.Group);
        data.Group = args.Group;
        data.Leader = args.Group == FactionMemberGroup.None ? false : data.Leader;
        if (comp.MenuAccess != FactionMenuAccess.Full)
            comp.MenuAccess = args.Group == FactionMemberGroup.None ? FactionMenuAccess.None : (data.Leader ? FactionMenuAccess.Group : FactionMenuAccess.None);

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/faction_group_assigned.ogg"), uid.Value);
        _popup.PopupEntity("Вам была назначена новая группа.", uid.Value, uid.Value, Shared.Popups.PopupType.Medium);

        Dirty(uid.Value, comp);
        RefreshFactionMenu(comp.Faction);
    }

    private void OnMemberRemoved(RemoveFactionMemberMessage args)
    {
        if (!GetFactionMemberById(args.Ent, out var uid))
        {
            if (!TryGetFactionMemberData(args.Ent, out var data) || !EnsureFactionDataContainer(out var cont))
                return;
            var fact = data.Faction;
            cont.Value.Comp.CachedMembers.GetOrNew(data.Faction).Remove(args.Ent);
            RefreshFactionMenu(fact);

            return;
        }
        if (!TryGetFactionMemberData(args.Performer, out var headData))
            return;
        if (TryGetFactionMemberData(args.Ent, out var memberdata))
        {
            _entity.GetEntitySystem<CombatStancePointTestSystem>().MemberRemoved(uid.Value, memberdata.Faction, memberdata.Group);
        }
        if (args.Headhunt)
        {
            if (TryComp<MedievalFactionMemberComponent>(uid, out var comp) && _mind.TryGetMind(uid.Value, out var mindId, out _) && _job.MindTryGetJob(mindId, out var job))
                AddWanted(uid.Value, job.ID, headData.Name, args.Details, comp.Faction);
        }

        SetJob(uid.Value, "Voluntary", "Нет должности");
    }

    private void OnSetLeader(SetGroupLeaderMessage args)
    {
        if (!GetFactionMemberById(args.Ent, out var uid))
            return;
        if (!uid.Value.IsValid())
            return;
        if (!TryComp<MedievalFactionMemberComponent>(uid, out var comp))
            return;
        if (!TryGetFactionMemberData(args.Ent, out var data))
            return;

        data.Leader = args.Leader;
        comp.MenuAccess = args.Leader ? FactionMenuAccess.Group : FactionMenuAccess.None;
        Dirty(uid.Value, comp);
        RefreshFactionMenu(comp.Faction);
    }

    public void SetJob(EntityUid uid, ProtoId<MedievalFactionPrototype> faction, string job, string jobPrefix = "")
    {
        var comp = EnsureComp<MedievalFactionMemberComponent>(uid);
        var oldFaction = comp.Faction;

        if (!EnsureFactionDataContainer(out var container))
            return;
        if (!TryGetFactionMemberData(comp.MemberID, out var data))
            return;

        data.Group = FactionMemberGroup.None;
        data.Job = job;
        data.JobPrefix = jobPrefix;
        data.Faction = faction;
        comp.Faction = faction;
        if (Proto.TryIndex(oldFaction, out var factProto) && factProto.WantedText != null && !factProto.AllowHeadhunt)
            comp.Wanted = new(oldFaction, factProto.WantedText);

        container.Value.Comp.CachedMembers.GetOrNew(oldFaction).Remove(comp.MemberID);
        container.Value.Comp.CachedMembers.GetOrNew(faction).Add(comp.MemberID, data);
        Dirty(uid, comp);
        RefreshFactionMenu(faction);
        RefreshFactionMenu(oldFaction);
    }

    public void RefreshFactionMenu(ProtoId<MedievalFactionPrototype> proto)
    {
        Dictionary<int, FactionMemberData> dict = new();

        if (!EnsureFactionDataContainer(out var container))
            return;

        dict = container.Value.Comp.CachedMembers.GetOrNew(proto);
        var list = dict.ToList();
        list.Sort((x, y) =>
        {
            if (!GetFactionMemberById(x.Key, out var xEnt) || !TryComp<MedievalFactionMemberComponent>(xEnt, out var xFriends))
                return 1;
            if (!GetFactionMemberById(y.Key, out var yEnt) || !TryComp<MedievalFactionMemberComponent>(yEnt, out var yFriends))
                return -1;

            return yFriends.Priority.CompareTo(xFriends.Priority);
        });

        var headQuery = EntityQueryEnumerator<FactionDataContainerComponent>();
        while (headQuery.MoveNext(out var uid, out var data))
        {
            if (data.CachedMembers.TryGetValue(proto, out _))
                data.CachedMembers[proto] = list.ToDictionary();
            else
                data.CachedMembers.Add(proto, list.ToDictionary());
            Dirty(uid, data);
        }
    }

    public void UpdateGoals(Entity<FactionDataContainerComponent> cont)
    {
        foreach (var item in cont.Comp.Goals)
        {
            foreach (var goal in item.Value)
            {
                goal.Progress = goal.Completer.GetCompletion(EntityManager);
            }
        }

        Dirty(cont);
    }

    public bool EnsureFactionDataContainer([NotNullWhen(true)] out Entity<FactionDataContainerComponent>? ent)
    {
        ent = null;
        var query = EntityQueryEnumerator<FactionDataContainerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            ent = (uid, comp);
            return true;
        }
        var ensureQuery = EntityQueryEnumerator<BecomesStationComponent>();
        while (ensureQuery.MoveNext(out var uid, out var comp))
        {
            var data = EnsureComp<FactionDataContainerComponent>(uid);
            ent = (uid, data);
            return true;
        }

        return false;
    }
}
