using System.Numerics;
using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

public sealed class CultBloodRitesWindow : DefaultWindow
{
    public event Action<CultBloodRitesMode>? OnModeSelected;

    private readonly Label _charges;

    public CultBloodRitesWindow()
    {
        Title = Loc.GetString("cult-blood-rites-window-title");
        MinSize = new Vector2(360, 260);

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };

        _charges = new Label();
        vbox.AddChild(_charges);

        AddModeButton(vbox, CultBloodRitesMode.Gather, "cult-blood-rites-mode-gather");
        AddModeButton(vbox, CultBloodRitesMode.Heal, "cult-blood-rites-mode-heal");
        AddModeButton(vbox, CultBloodRitesMode.Recharge, "cult-blood-rites-mode-recharge");
        AddModeButton(vbox, CultBloodRitesMode.Orb, "cult-blood-rites-mode-orb");
        AddModeButton(vbox, CultBloodRitesMode.Spear, "cult-blood-rites-mode-spear");

        Contents.AddChild(vbox);
    }

    public void SetState(CultBloodRitesMode mode, int charges)
    {
        _charges.Text = Loc.GetString("cult-blood-rites-window-charges", ("charges", charges), ("mode", Loc.GetString(GetModeLoc(mode))));
    }

    private void AddModeButton(BoxContainer parent, CultBloodRitesMode mode, string loc)
    {
        var btn = new Button
        {
            Text = Loc.GetString(loc),
            HorizontalExpand = true,
            MinSize = new Vector2(0, 36),
        };

        btn.OnPressed += _ => OnModeSelected?.Invoke(mode);
        parent.AddChild(btn);
    }

    private static string GetModeLoc(CultBloodRitesMode mode) => mode switch
    {
        CultBloodRitesMode.Gather => "cult-blood-rites-mode-gather",
        CultBloodRitesMode.Heal => "cult-blood-rites-mode-heal",
        CultBloodRitesMode.Recharge => "cult-blood-rites-mode-recharge",
        CultBloodRitesMode.Orb => "cult-blood-rites-mode-orb",
        CultBloodRitesMode.Spear => "cult-blood-rites-mode-spear",
        _ => "cult-blood-rites-mode-gather",
    };
}
