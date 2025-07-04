using Content.Client.Imperial.Medieval.Friends.UI;
using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Friends;

public sealed partial class FriendsSystem : SharedFriendsSystem
{
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public static Dictionary<FactionMemberGroup, Color> GroupColors = new()
    {
        { FactionMemberGroup.None, Color.FromHex("#1f1f24") },
        { FactionMemberGroup.Alpha, Color.FromHex("#794646") },
        { FactionMemberGroup.Bravo, Color.FromHex("#4E4679") },
        { FactionMemberGroup.Delta, Color.FromHex("#467953") },
        { FactionMemberGroup.Gamma, Color.FromHex("#797746") },
        { FactionMemberGroup.Omega, Color.FromHex("#6F4679") },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FactionDataContainerComponent, AfterAutoHandleStateEvent>(AfterAutoHandleState);
        SubscribeNetworkEvent<OpenOfferFactionRelationsEvent>(OnOpenOfferWindow);
        SubscribeNetworkEvent<OpenAcceptFactionRelationsEvent>(OnOpenAcceptWindow);
    }

    private void AfterAutoHandleState(EntityUid uid, FactionDataContainerComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!_player.LocalEntity.HasValue)
            return;
        var player = _player.LocalEntity.Value;
        if (!TryComp<FriendsComponent>(player, out var friends))
            return;
        if (!TryGetFactionDataContainer(out var container) || !container.Value.Comp.CachedMembers.TryGetValue(friends.Faction, out var val))
            return;

        TryGetFactionMemberData(friends.MemberID, out var selfData);

        FactionMenuData menuData = new(friends.Faction, val,
                                    comp.Relations,
                                    friends.MenuAccess,
                                    selfData?.Group ?? FactionMemberGroup.None,
                                    friends.MemberID);

        _uiMan.GetUIController<FactionMenuUiController>().PopulateMenu(menuData);
    }

    private void OnOpenOfferWindow(OpenOfferFactionRelationsEvent ev)
    {
        _uiMan.GetUIController<FactionRelationsUiController>().OpenSetRelationMenu(ev.Target, ev.UserFaction, ev.TargetFaction);
    }

    private void OnOpenAcceptWindow(OpenAcceptFactionRelationsEvent ev)
    {
        _uiMan.GetUIController<FactionRelationsUiController>().OpenAcceptMenu(ev.UserFaction, ev.TargetFaction, ev.Relation);
    }

    public override void OpenMenu(ProtoId<MedievalFactionPrototype> proto, Dictionary<int, FactionMemberData> data, FactionMenuAccess access)
    {
        if (_time.IsFirstTimePredicted)
        {
            if (!TryComp<FriendsComponent>(_player.LocalEntity, out var friends))
                return;

            _uiMan.GetUIController<FactionMenuUiController>().ToggleMenu();
            if (!TryGetFactionDataContainer(out var container) || !container.Value.Comp.CachedMembers.TryGetValue(proto, out var val))
                return;

            TryGetFactionMemberData(friends.MemberID, out var selfData);

            FactionMenuData menuData = new(friends.Faction, val,
                                        container.Value.Comp.Relations,
                                        friends.MenuAccess,
                                        selfData?.Group ?? FactionMemberGroup.None,
                                        friends.MemberID);

            _uiMan.GetUIController<FactionMenuUiController>().PopulateMenu(menuData);
        }
    }
}
