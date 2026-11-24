using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.Imperial.SyndieBattle.Events;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.SyndieBattle.UI;

public sealed partial class SyndieBattleStoreUi : UIFragment
{
    private SyndieBattleStoreUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new SyndieBattleStoreUiFragment();
        _fragment.OnOpenStore += () => userInterface.SendMessage(new CartridgeUiMessage(new SyndieBattleOpenStoreEvent()));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
    }
}
