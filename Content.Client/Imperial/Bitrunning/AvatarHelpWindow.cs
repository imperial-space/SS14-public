using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Bitrunning;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

/// <summary>Действие «Информация о домене» открывает окно AvatarHelp.</summary>
public sealed class BitrunningAvatarHelpSystem : EntitySystem
{
    private AvatarHelpWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BitrunningAvatarHelpEvent>(OnHelp);
    }

    private void OnHelp(BitrunningAvatarHelpEvent ev)
    {
        _window?.Close();
        _window = new AvatarHelpWindow(ev.Help);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}

/// <summary>Окно AvatarHelp 1 в 1 с tgui (600×600): подсказка домена и шесть карточек правил.</summary>
public sealed class AvatarHelpWindow : DefaultWindow
{
    private static readonly (string Color, string Icon, string Key)[] Boxes =
    {
        ("#a333c8", "search-location", "search"),
        ("#21ba45", "boxes", "cache"),
        ("#2185d0", "plug", "disconnect"),
        ("#fbbd08", "id-badge", "safety"),
        ("#e9b934", "coins", "retries"),
        ("#db2828", "skull-crossbones", "danger"),
    };

    public AvatarHelpWindow(string help)
    {
        Title = Loc.GetString("bitrunning-help-title");
        MinSize = SetSize = new Vector2(600, 600);

        var root = Tgui.VBox(6);
        root.Margin = new Thickness(6);
        root.VerticalExpand = true;

        var text = Tgui.Paragraph(help, Tgui.Good);
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(text);
        var welcome = Tgui.MakeSection(Loc.GetString("bitrunning-help-welcome"), null, scroll);
        welcome.VerticalExpand = true;
        welcome.SizeFlagsStretchRatio = 1;
        root.AddChild(welcome);

        var grid = Tgui.VBox(6);
        grid.VerticalExpand = true;
        grid.SizeFlagsStretchRatio = 4;
        for (var row = 0; row < 3; row++)
        {
            var line = Tgui.HBox(6, BoxHelp(row * 2), BoxHelp(row * 2 + 1));
            line.VerticalExpand = true;
            grid.AddChild(line);
        }

        root.AddChild(grid);
        Contents.AddChild(Tgui.Window(root, scrollable: false));
    }

    private static Control BoxHelp(int index)
    {
        var (color, icon, key) = Boxes[index];
        var title = Tgui.HBox(6,
            BitrunningTgui.Icon(icon, Color.FromHex(color), 14),
            Tgui.Text(Loc.GetString($"bitrunning-help-{key}-title"), size: 14, bold: true));

        var body = Tgui.Paragraph(Loc.GetString($"bitrunning-help-{key}"), Tgui.Label);
        body.Margin = new Thickness(6, 8, 6, 8);

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        title.Margin = new Thickness(6, 5, 6, 5);
        header.AddChild(title);
        header.AddChild(new PanelContainer
        {
            MinHeight = 2,
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = Tgui.PrimaryLine },
        });
        header.AddChild(body);

        return new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinHeight = 120,
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = Tgui.Section },
            Children = { header },
        };
    }
}
