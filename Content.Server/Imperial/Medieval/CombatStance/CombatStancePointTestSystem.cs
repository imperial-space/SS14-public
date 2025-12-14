using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Robust.Shared.Timing;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Content.Shared.Imperial.Medieval.CombatStance;
using Robust.Shared.Physics.Events;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Server.Imperial.Medieval.Factions;
using Content.Shared.Mobs;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Humanoid;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Server.GameStates;
using Robust.Shared.Player;
using Content.Server.Imperial.PVS;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Events;
using Content.Shared.Explosion;
using Content.Server.Popups;

namespace Content.Server.Imperial.Medieval.CombatStance;

public sealed class CombatStancePointTestSystem : EntitySystem
{
    [Dependency] private readonly MedievalFactionsSystem _friends = default!;
    [Dependency] private readonly EntityLookupSystem _look = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly AlwaysPvsSystem _pvs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<CombatStancePointComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<CombatStancePointComponent, EndCollideEvent>(EndCollide);
        SubscribeLocalEvent<MedievalFactionMemberComponent, CombatStancePointEvent>(Prompt);
        _net.RegisterNetMessage<CombatStancePointPlaceMessage>(PlacePoint);
        _net.RegisterNetMessage<CombatStancePointRemoveMessage>(RemovePoints);
        SubscribeLocalEvent<CombatStanceComponent, DamageModifyEvent>(ModifyDamage);
        SubscribeLocalEvent<CombatStanceComponent, StaminaModifyEvent>(StaminaModifyDamage);
        SubscribeLocalEvent<CombatStanceComponent, GetExplosionResistanceEvent>(ExplosionDamage);
        SubscribeLocalEvent<CombatStancePointComponent, ComponentShutdown>(JustIncase);
        SubscribeLocalEvent<CombatStanceComponent, AnchorStateChangedEvent>(BlockBegin);
    }
    private void JustIncase(EntityUid uid, CombatStancePointComponent component, ComponentShutdown args)
    {
        foreach (var member in component.ValidMembers)
        {
            if (Deleted(member))
                continue;
            if (TryComp<CombatStanceComponent>(member, out var stance))
                stance.HasDefence = false;
        }
    }
    private void BlockBegin(EntityUid uid, CombatStanceComponent component, AnchorStateChangedEvent args)
    {
        if (!TryComp<MedievalFactionMemberComponent>(uid, out var friends))
            return;
        if (!_friends.TryGetFactionMemberData(friends.MemberID, out var data))
            return;
        foreach (var ent in _look.GetEntitiesInRange(Transform(uid).Coordinates, 0.7f))
        {
            if (!TryComp<CombatStancePointComponent>(ent, out var point))
                continue;
            OnCollide(ent, point, uid);
            return;
        }
    }
    private void ExplosionDamage(EntityUid uid, CombatStanceComponent component, ref GetExplosionResistanceEvent args)
    {
        if (!component.HasDefence)
            return;
        args.DamageCoefficient *= 0.25f;
    }
    private void StaminaModifyDamage(EntityUid uid, CombatStanceComponent component, StaminaModifyEvent args)
    {
        if (!component.HasDefence)
            return;
        if (args.Damage > 0)
            args.Damage = args.Damage * 0.5f;
    }
    private void ModifyDamage(EntityUid uid, CombatStanceComponent component, DamageModifyEvent args)
    {
        if (!component.HasDefence)
            return;
        var newdict = new Dictionary<string, FixedPoint2>();
        foreach (var (key, value) in args.Damage.DamageDict)
        {
            Console.WriteLine(value.Float());
            if (value > 0)
                newdict[key] = FixedPoint2.New(value.Float() * 0.5f);
            else
                newdict[key] = value;
            Console.WriteLine(newdict[key].Float());
        }
        args.Damage.DamageDict = newdict;
    }
    public void MemberRemoved(EntityUid uid, string faction, FactionMemberGroup group)
    {
        if (!_friends.TryGetFactionDataContainer(out var factions))
            return;
        if (!_players.TryGetSessionByEntity(uid, out var session))
            return;
        if (!TryComp<MedievalFactionMemberComponent>(uid, out var friends))
            return;
        if (friends.MenuAccess == FactionMenuAccess.Full)
            return;
        foreach (var point in factions.Value.Comp.Points.GetOrNew(faction).GetOrNew(group))
        {
            _pvs.RemoveForceSend(point, session);
        }
    }
    public void GroupChanged(EntityUid uid, string faction, FactionMemberGroup group, FactionMemberGroup oldgroup)
    {
        if (!_friends.TryGetFactionDataContainer(out var factions))
            return;
        if (!_players.TryGetSessionByEntity(uid, out var session))
            return;
        if (oldgroup != FactionMemberGroup.None)
        {
            foreach (var point in factions.Value.Comp.Points.GetOrNew(faction).GetOrNew(oldgroup))
            {
                _pvs.RemoveForceSend(point, session);
            }
        }
        if (group != FactionMemberGroup.None)
        {
            foreach (var point in factions.Value.Comp.Points.GetOrNew(faction).GetOrNew(group))
            {
                _pvs.AddForceSend(point, session);
            }
        }
    }
    private void RemovePoints(CombatStancePointRemoveMessage msg)
    {
        var player = _players.GetSessionByChannel(msg.MsgChannel);
        var uid = player.AttachedEntity;
        if (uid == null)
            return;
        if (!TryComp<MedievalFactionMemberComponent>(uid, out var friends))
            return;
        if (friends.MenuAccess != FactionMenuAccess.Full)
            return;
        if (!_friends.TryGetFactionDataContainer(out var factions))
            return;
        var points = factions.Value.Comp.Points.GetOrNew(friends.Faction).GetOrNew(msg.Group);
        foreach (var point in points)
        {
            if (Deleted(point))
                continue;
            QueueDel(point);
        }
        points.Clear();
    }
    private void PlacePoint(CombatStancePointPlaceMessage msg)
    {
        var player = _players.GetSessionByChannel(msg.MsgChannel);
        var uid = player.AttachedEntity;
        if (uid == null)
            return;
        if (!TryComp<MedievalFactionMemberComponent>(uid, out var friends))
            return;
        if (friends.MenuAccess != FactionMenuAccess.Full)
            return;
        if (!_friends.TryGetFactionDataContainer(out var factions))
            return;
        if (!factions.Value.Comp.CachedMembers.TryGetValue(friends.Faction, out var members))
            return;
        var pvslist = new List<ICommonSession>() { player };
        foreach (var (id, member) in members)
        {
            if (id == friends.MemberID)
                continue;
            if (member.Group != msg.Group)
                continue;
            if (!_friends.GetFactionMemberById(id, out var memberuid))
                continue;
            if (!_players.TryGetSessionByEntity(memberuid.Value, out var membersession))
                continue;
            pvslist.Add(membersession);
        }
        var points = factions.Value.Comp.Points.GetOrNew(friends.Faction).GetOrNew(msg.Group);
        var toremove = new List<EntityUid>();
        var count = 0;
        foreach (var point in points)
        {
            if (Deleted(point))
            {
                toremove.Add(point);
                continue;
            }
            count++;
        }
        foreach (var point in toremove)
        {
            points.Remove(point);
        }
        if (count >= pvslist.Count)
        {
            _popup.PopupEntity(Loc.GetString("medieval-cant-place-toomuch-stancepoints"), uid.Value, uid.Value);
            return;
        }
        var ent = Spawn("StancePoint", EntityManager.GetCoordinates(msg.Coords));
        foreach (var session in pvslist)
        {
            _pvs.AddForceSend(ent, session);
        }
        var newpoint = EnsureComp<CombatStancePointComponent>(ent);
        newpoint.Group = msg.Group;
        newpoint.Faction = friends.Faction;
        points.Add(ent);
        Init(ent, newpoint);
    }
    private void Prompt(EntityUid uid, MedievalFactionMemberComponent component, CombatStancePointEvent args)
    {
        if (!_players.TryGetSessionByEntity(uid, out var session))
            return;
        RaiseNetworkEvent(new CombatStanceMenuEvent(), session);
    }
    private bool TryMatch(EntityUid uid, CombatStancePointComponent ourcomponent, int x, int y, [NotNullWhen(true)] out EntityUid? result)
    {
        result = null;
        var coords = Transform(uid).MapPosition.Offset(x, y);
        foreach (var ent in _look.GetEntitiesInRange(coords, 0.7f, LookupFlags.All))
        {
            if (ent == uid)
                continue;
            if (!TryComp<CombatStancePointComponent>(ent, out var component))
                continue;

            if (component.Faction != ourcomponent.Faction || component.Group != ourcomponent.Group)
                continue;

            result = ent;
            return true;
        }

        return false;
    }
    private void Init(EntityUid uid, CombatStancePointComponent component)
    {
        if (TryMatch(uid, component, 1, 0, out var rightresult))
        {
            component.PointDirection.right = true;
            component.PointDirectionData.right = rightresult;
            var othercomp = EnsureComp<CombatStancePointComponent>(rightresult.Value);
            othercomp.PointDirection.left = true;
            othercomp.PointDirectionData.left = uid;
        }
        if (TryMatch(uid, component, -1, 0, out var leftresult))
        {
            component.PointDirection.left = true;
            component.PointDirectionData.left = leftresult;
            var othercomp = EnsureComp<CombatStancePointComponent>(leftresult.Value);
            othercomp.PointDirection.right = true;
            othercomp.PointDirectionData.right = uid;
        }
        if (TryMatch(uid, component, 0, 1, out var upresult))
        {
            component.PointDirection.up = true;
            component.PointDirectionData.up = upresult;
            var othercomp = EnsureComp<CombatStancePointComponent>(upresult.Value);
            othercomp.PointDirection.bottom = true;
            othercomp.PointDirectionData.bottom = uid;
        }
        if (TryMatch(uid, component, 0, -1, out var bottomresult))
        {
            component.PointDirection.bottom = true;
            component.PointDirectionData.bottom = bottomresult;
            var othercomp = EnsureComp<CombatStancePointComponent>(bottomresult.Value);
            othercomp.PointDirection.up = true;
            othercomp.PointDirectionData.up = uid;
        }
    }

    private void OnCollide(EntityUid uid, CombatStancePointComponent component, StartCollideEvent args)
    {
        OnCollide(uid, component, args.OtherEntity);
    }
    private void OnCollide(EntityUid uid, CombatStancePointComponent component, EntityUid other)
    {
        if (!TryComp<MedievalFactionMemberComponent>(other, out var friends))
            return;
        if (!_friends.TryGetFactionMemberData(friends.MemberID, out var data))
            return;
        if (data.Group != component.Group || data.Faction != component.Faction)
            return;
        if (!HasComp<CombatStanceComponent>(other))
            return;
        if (component.ValidMembers.Contains(other))
            return;
        component.ValidMembers.Add(other);
        ListChanged(uid, component);
    }
    private void EndCollide(EntityUid uid, CombatStancePointComponent component, EndCollideEvent args)
    {
        if (!component.ValidMembers.Contains(args.OtherEntity))
            return;
        component.ValidMembers.Remove(args.OtherEntity);
        if (TryComp<CombatStanceComponent>(args.OtherEntity, out var comp))
            comp.HasDefence = false;
        ListChanged(uid, component);
    }
    public List<RecursivePointData> Recursive(EntityUid uid, CombatStancePointComponent component, List<RecursivePointData> currentlist, ref RecursivePointData currentdata, List<EntityUid> blacklist)
    {
        blacklist.Add(uid);
        if (component.HasValidMember)
        {
            if (currentdata.Negative)
            {
                currentlist.Add(currentdata);
                currentdata = new();
            }
            currentdata.Points.Add(uid);
            foreach (var part in component.ValidMembers)
            {
                if (currentdata.Participants.Contains(part))
                    continue;
                currentdata.AmountOfValid++;
                currentdata.Participants.Add(part);
                break;
            }
        }
        else
        {
            if (currentdata.AmountOfValid == 0)
            {
                currentdata.Negative = true;
                currentdata.Points.Add(uid);
            }
            else
            {
                currentlist.Add(currentdata);
                currentdata = new();
            }
        }
        if (component.PointDirection.up && component.PointDirectionData.up.HasValue && !Deleted(component.PointDirectionData.up) && !blacklist.Contains(component.PointDirectionData.up.Value))
        {
            currentlist = Recursive(component.PointDirectionData.up.Value, EnsureComp<CombatStancePointComponent>(component.PointDirectionData.up.Value), currentlist, ref currentdata, blacklist);
        }
        if (component.PointDirection.bottom && component.PointDirectionData.bottom.HasValue && !Deleted(component.PointDirectionData.bottom) && !blacklist.Contains(component.PointDirectionData.bottom.Value))
        {
            currentlist = Recursive(component.PointDirectionData.bottom.Value, EnsureComp<CombatStancePointComponent>(component.PointDirectionData.bottom.Value), currentlist, ref currentdata, blacklist);
        }
        if (component.PointDirection.right && component.PointDirectionData.right.HasValue && !Deleted(component.PointDirectionData.right) && !blacklist.Contains(component.PointDirectionData.right.Value))
        {
            currentlist = Recursive(component.PointDirectionData.right.Value, EnsureComp<CombatStancePointComponent>(component.PointDirectionData.right.Value), currentlist, ref currentdata, blacklist);
        }
        if (component.PointDirection.left && component.PointDirectionData.left.HasValue && !Deleted(component.PointDirectionData.left) && !blacklist.Contains(component.PointDirectionData.left.Value))
        {
            currentlist = Recursive(component.PointDirectionData.left.Value, EnsureComp<CombatStancePointComponent>(component.PointDirectionData.left.Value), currentlist, ref currentdata, blacklist);
        }
        return currentlist;
    }
    private void ListChanged(EntityUid uid, CombatStancePointComponent component)
    {
        var lastdata = new RecursivePointData();
        var result = Recursive(uid, component, new(), ref lastdata, new());
        result.Add(lastdata);
        foreach (var data in result)
        {
            foreach (var point in data.Points)
            {
                var comp = EnsureComp<CombatStancePointComponent>(point);
                comp.Participants = data.AmountOfValid;
                UpdateState(point, comp);
            }
            foreach (var participant in data.Participants)
            {
                var comp = EnsureComp<CombatStanceComponent>(participant);
                if (data.AmountOfValid < 4)
                    comp.HasDefence = false;
                else
                    comp.HasDefence = true;
            }
        }
    }
    private void UpdateState(EntityUid uid, CombatStancePointComponent component)
    {
        if (component.Participants <= 0)
        {
            _appearance.SetData(uid, CombatStanceAppearance.Key, 0);
            return;
        }
        if (component.Participants >= 4)
        {
            _appearance.SetData(uid, CombatStanceAppearance.Key, 2);
            return;
        }
        if (component.Participants < 4)
        {
            _appearance.SetData(uid, CombatStanceAppearance.Key, 1);
            return;
        }
    }
}

public class RecursivePointData
{
    public List<EntityUid> Points = new();
    public List<EntityUid> Participants = new();
    public int AmountOfValid = 0;
    public bool Negative = false;
}
