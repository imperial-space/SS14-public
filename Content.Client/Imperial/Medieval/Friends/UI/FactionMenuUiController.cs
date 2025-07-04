using Content.Client.Friends;
using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Friends.UI;

[UsedImplicitly]
public sealed class FactionMenuUiController : UIController
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private FactionMenu? _menu;

    public void ToggleMenu()
    {
        if (_menu == null)
        {
            // setup window
            _menu = UIManager.CreateWindow<FactionMenu>();
            _menu.OnClose += () => _menu = null;

            _menu.ObjectiveSet += ObjectiveSet;
            _menu.GroupSet += GroupSet;
            _menu.SetLeaderPressed += SetLeader;
            _menu.FirePressed += args => Fire(args, "", false);
            _menu.HeadhuntPressed += (id, details) => Fire(id, details, true);
            _menu.WarPressed += DispatchWar;

            _menu.OpenCentered();
        }
        else
        {
            _menu.ObjectiveSet -= ObjectiveSet;
            _menu.GroupSet -= GroupSet;
            _menu.SetLeaderPressed -= SetLeader;
            _menu.FirePressed -= args => Fire(args, "", false);
            _menu.HeadhuntPressed -= (id, details) => Fire(id, details, true);
            _menu.WarPressed -= DispatchWar;

            _menu.Close();
            _menu = null;
        }
    }

    public void PopulateMenu(FactionMenuData data)
    {
        if (_menu == null)
            return;

        if (_menu.Mode == FactionMenu.MenuMode.Relations)
            _menu.PopulateRelations(data);
        else
            _menu.Populate(data);
    }

    private void Fire(int ent, string details, bool headhunt = false)
    {
        var playerMan = IoCManager.Resolve<IPlayerManager>();
        if (_entityManager.TryGetComponent<FriendsComponent>(playerMan.LocalEntity, out var friends))
            _entityManager.RaisePredictiveEvent(new RemoveFactionMemberMessage(ent, friends.MemberID, details, headhunt));
    }

    private void ObjectiveSet(FactionMemberGroup group, string obj)
    {
        if (_menu == null)
            return;

        _entityManager.RaisePredictiveEvent(new SetFactionMemberObjectiveMessage(_menu.Data.Faction, group, obj));
    }

    private void GroupSet(int ent, FactionMemberGroup obj)
    {
        _entityManager.RaisePredictiveEvent(new SetFactionMemberGroupMessage(ent, obj));
    }

    private void SetLeader(int ent, bool leader)
    {
        _entityManager.RaisePredictiveEvent(new SetGroupLeaderMessage(ent, leader));
    }

    private void DispatchWar(ProtoId<MedievalFactionPrototype> faction)
    {
        if (_menu == null)
            return;

        _entityManager.RaisePredictiveEvent(new DispatchWarEvent(_menu.Data.Faction, faction));
    }
}
