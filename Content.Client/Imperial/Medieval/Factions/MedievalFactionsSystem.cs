using Content.Client.Imperial.Medieval.Factions.UI;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Factions;

public sealed partial class MedievalFactionsSystem : SharedMedievalFactionsSystem
{
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public static Dictionary<FactionMemberGroup, Color> GroupColors = new()
    {
        { FactionMemberGroup.None, Color.FromHex("#1f1f24") },
        { FactionMemberGroup.Alpha, Color.FromHex("#794646") },
        { FactionMemberGroup.Bravo, Color.FromHex("#4E4679") },
        { FactionMemberGroup.Delta, Color.FromHex("#467953") },
        { FactionMemberGroup.Gamma, Color.FromHex("#797746") },
        { FactionMemberGroup.Omega, Color.FromHex("#6F4679") },
    };

    private ProtoId<FactionIconPrototype> _friendIcon = "FactionFriend";
    private ProtoId<FactionIconPrototype> _headIcon = "FactionHead";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FactionDataContainerComponent, AfterAutoHandleStateEvent>(AfterAutoHandleState);

        SubscribeLocalEvent<MedievalFactionMemberComponent, GetStatusIconsEvent>(OnGetStatusIcons);

        SubscribeNetworkEvent<OpenOfferFactionRelationsEvent>(OnOpenOfferWindow);
        SubscribeNetworkEvent<OpenAcceptFactionRelationsEvent>(OnOpenAcceptWindow);
        SubscribeNetworkEvent<OpenFactionRelationsRequestEvent>(OnOpenRequestWindow);
    }

    private void AfterAutoHandleState(EntityUid uid, FactionDataContainerComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!_player.LocalEntity.HasValue)
            return;
        var player = _player.LocalEntity.Value;
        if (!TryComp<MedievalFactionMemberComponent>(player, out var friends))
            return;
        if (!TryGetFactionDataContainer(out var container) || !container.Value.Comp.CachedMembers.TryGetValue(friends.Faction, out var val))
            return;

        TryGetFactionMemberData(friends.MemberID, out var selfData);

        FactionMenuData menuData = new(friends.Faction, val,
                                    comp.Relations,
                                    friends.MenuAccess,
                                    selfData?.Group ?? FactionMemberGroup.None,
                                    container.Value.Comp.Goals.GetOrNew(friends.Faction),
                                    friends.MemberID);

        _uiMan.GetUIController<FactionMenuUiController>().PopulateMenu(menuData);
    }

    private void OnGetStatusIcons(EntityUid uid, MedievalFactionMemberComponent comp, ref GetStatusIconsEvent args)
    {
        if (uid == _player.LocalEntity)
            return;

        if (!TryComp<MedievalFactionMemberComponent>(_player.LocalEntity, out var playerFaction))
            return;

        if (comp.Faction != playerFaction.Faction)
            return;

        if (Shared.IdentityManagement.Identity.Name(uid, EntityManager, _player.LocalEntity) != Name(uid))
            return;

        var iconId = comp.MenuAccess == FactionMenuAccess.Full ? _headIcon : _friendIcon;
        args.StatusIcons.Add(_proto.Index(iconId));
    }

    private void OnOpenOfferWindow(OpenOfferFactionRelationsEvent ev)
    {
        _uiMan.GetUIController<FactionRelationsUiController>().OpenSetRelationMenu(ev.Target, ev.UserFaction, ev.TargetFaction);
    }

    private void OnOpenAcceptWindow(OpenAcceptFactionRelationsEvent ev)
    {
        _uiMan.GetUIController<FactionRelationsUiController>().OpenAcceptMenu(ev.UserFaction, ev.TargetFaction, ev.Relation);
    }

    private void OnOpenRequestWindow(OpenFactionRelationsRequestEvent ev)
    {
        _uiMan.GetUIController<FactionRelationsUiController>().OpenRequestRelationMenu(ev.Target, ev.From);
    }

    public override void OpenMenu(ProtoId<MedievalFactionPrototype> proto, Dictionary<int, FactionMemberData> data, FactionMenuAccess access)
    {
        if (_time.IsFirstTimePredicted)
        {
            if (!TryComp<MedievalFactionMemberComponent>(_player.LocalEntity, out var friends))
                return;

            _uiMan.GetUIController<FactionMenuUiController>().ToggleMenu();
            if (!TryGetFactionDataContainer(out var container) || !container.Value.Comp.CachedMembers.TryGetValue(proto, out var val))
                return;

            TryGetFactionMemberData(friends.MemberID, out var selfData);

            FactionMenuData menuData = new(friends.Faction, val,
                                        container.Value.Comp.Relations,
                                        friends.MenuAccess,
                                        selfData?.Group ?? FactionMemberGroup.None,
                                        container.Value.Comp.Goals.GetOrNew(friends.Faction),
                                        friends.MemberID);

            _uiMan.GetUIController<FactionMenuUiController>().PopulateMenu(menuData);
        }
    }
}
