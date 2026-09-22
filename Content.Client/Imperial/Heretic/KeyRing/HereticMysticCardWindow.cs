using System.Numerics;
using Content.Shared.Imperial.Heretic.KeyRing;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Heretic.KeyRing;

public sealed class HereticMysticCardWindow : DefaultWindow
{
    public event Action<string>? OnCardSelected;

    private readonly BoxContainer _list;
    private readonly Label _invertLabel;

    private static readonly Color ColorTitle = Color.FromHex("#cc4444");
    private static readonly Color ColorAccess = Color.FromHex("#888888");

    public HereticMysticCardWindow()
    {
        Title = Loc.GetString("heretic-mystic-card-window-title");
        MinSize = new Vector2(340, 260);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };

        _invertLabel = new Label { FontColorOverride = ColorAccess };
        root.AddChild(_invertLabel);

        root.AddChild(new Label
        {
            Text = Loc.GetString("heretic-mystic-card-window-choose"),
            FontColorOverride = ColorTitle,
            Margin = new Thickness(0, 0, 0, 2),
        });

        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        Contents.AddChild(root);
    }

    public void Populate(HereticMysticCardBuiState state)
    {
        _invertLabel.Text = Loc.GetString(state.Inverted
            ? "heretic-mystic-card-inverted-label"
            : "heretic-mystic-card-normal-label");

        _list.RemoveAllChildren();

        foreach (var card in state.Cards)
        {
            var capturedName = card.Name;

            var cardBox = new PanelContainer
            {
                Margin = new Thickness(0, 0, 0, 2),
            };

            var inner = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(4),
                SeparationOverride = 2,
            };

            var btn = new Button
            {
                HorizontalExpand = true,
                Text = card.Name,
            };
            btn.Label.FontColorOverride = ColorTitle;
            btn.OnPressed += _ =>
            {
                OnCardSelected?.Invoke(capturedName);
                Close();
            };
            inner.AddChild(btn);

            var accessText = card.AccessTags.Count > 0
                ? Loc.GetString("heretic-mystic-card-access-list", ("tags", string.Join(", ", card.AccessTags)))
                : Loc.GetString("heretic-mystic-card-no-access");

            inner.AddChild(new Label
            {
                Text = accessText,
                FontColorOverride = ColorAccess,
                Margin = new Thickness(4, 0, 0, 0),
            });

            cardBox.AddChild(inner);
            _list.AddChild(cardBox);
        }
    }
}
