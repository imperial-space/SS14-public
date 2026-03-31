using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно выбора: призвать гомункулов или вознестись тёмным духом.
/// </summary>
public sealed class CultSpiritRealmWindow : DefaultWindow
{
    public event Action? OnHomunculiSelected;
    public event Action? OnSpiritSelected;

    public CultSpiritRealmWindow()
    {
        Title = Loc.GetString("cult-spirit-realm-window-title");
        MinSize = new Vector2(340, 180);

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 8,
        };

        var btnHomunculi = new Button
        {
            Text = Loc.GetString("cult-spirit-realm-option-homunculi"),
            HorizontalExpand = true,
            MinSize = new Vector2(0, 44),
        };
        btnHomunculi.OnPressed += _ =>
        {
            OnHomunculiSelected?.Invoke();
            Close();
        };

        var btnSpirit = new Button
        {
            Text = Loc.GetString("cult-spirit-realm-option-ascend"),
            HorizontalExpand = true,
            MinSize = new Vector2(0, 44),
        };
        btnSpirit.OnPressed += _ =>
        {
            OnSpiritSelected?.Invoke();
            Close();
        };

        vbox.AddChild(btnHomunculi);
        vbox.AddChild(btnSpirit);
        Contents.AddChild(vbox);
    }
}
