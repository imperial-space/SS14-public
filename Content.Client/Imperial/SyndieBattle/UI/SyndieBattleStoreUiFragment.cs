using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.SyndieBattle.UI;

public sealed class SyndieBattleStoreUiFragment : BoxContainer
{
    public event Action? OnOpenStore;

    public SyndieBattleStoreUiFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        AddChild(new Label { Text = Loc.GetString("syndiebattle-store-program-description"), HorizontalAlignment = HAlignment.Left, HorizontalExpand = true, Margin = new Thickness(8) });
        AddChild(new Label { Text = Loc.GetString("syndiebattle-store-program-balance-hint"), HorizontalAlignment = HAlignment.Left, HorizontalExpand = true });
        AddChild(new Label { Text = Loc.GetString("syndiebattle-store-program-redemption-hint"), HorizontalAlignment = HAlignment.Left, HorizontalExpand = true });
        AddChild(new Label { Text = Loc.GetString("syndiebattle-store-program-score-hint"), HorizontalAlignment = HAlignment.Left, HorizontalExpand = true });

        var openStoreButton = new Button { Text = Loc.GetString("syndiebattle-store-program-open"), HorizontalAlignment = HAlignment.Center, MinWidth = 220f, Margin = new Thickness(8) };

        openStoreButton.OnPressed += _ => OnOpenStore?.Invoke();
        AddChild(openStoreButton);
    }
}
