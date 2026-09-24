using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Imperial.Heretic;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticRitualWindow : DefaultWindow
{
    public event Action<string>?   OnRitualSelected;
    public event Action<NetEntity>? OnOfferingSelected;

    private readonly BoxContainer _list;
    private readonly SpriteSystem _spriteSystem;

    private static readonly Color TitlePurple = Color.FromHex("#d4a4ff");
    private static readonly Color OfferingRed  = Color.FromHex("#cc5555");
    private static readonly Color OfferingGold = Color.FromHex("#ddaa55");

    private static readonly SpriteSpecifier.Rsi RuneSpec = new(
        new ResPath("/Textures/Imperial/heretic/eldritch.rsi"),
        "cosmic_rune");

    public HereticRitualWindow()
    {
        _spriteSystem = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();

        Title   = Loc.GetString("heretic-ritual-window-title");
        MinSize = new Vector2(280, 180);

        // Style outer window panel
        foreach (var child in Children)
        {
            if (child is PanelContainer wp && wp.StyleClasses.Contains(StyleClassWindowPanel))
            {
                wp.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#0a0118") };
                break;
            }
        }
        WindowHeader.PanelOverride   = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1a0830") };
        TitleLabel.FontColorOverride = Color.FromHex("#d4a4ff");

        _list = new BoxContainer
        {
            Orientation        = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(_list);

        var gradPanel = new GradientPanel(6, 6);
        gradPanel.AddChild(scroll);

        Contents.AddChild(gradPanel);
    }

    private TextureRect MakeIcon(SpriteSpecifier? spec, int w, int h) => new()
    {
        Texture = _spriteSystem.Frame0(spec ?? RuneSpec),
        MinSize = new Vector2(w, h),
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
    };

    public void Populate(HereticRitualBuiState state)
    {
        _list.RemoveAllChildren();

        foreach (var ritual in state.Rituals)
        {
            var capturedId = ritual.Id;

            var row = new BoxContainer
            {
                Orientation        = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                HorizontalExpand   = true,
            };
            row.AddChild(MakeIcon(ritual.Icon, 24, 24));

            var btn = new Button { HorizontalExpand = true };
            btn.Label.Text              = ritual.Name;
            btn.Label.FontColorOverride = TitlePurple;
            btn.OnPressed += _ =>
            {
                OnRitualSelected?.Invoke(capturedId);
                Close();
            };
            row.AddChild(btn);
            _list.AddChild(row);
        }

        if (state.Offerings.Count > 0)
        {
            _list.AddChild(new PanelContainer
            {
                MinSize      = new Vector2(0, 1),
                Margin       = new Thickness(0, 4),
                PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#5c2888") },
            });
            _list.AddChild(new Label
            {
                Text              = Loc.GetString("heretic-offering-header"),
                FontColorOverride = OfferingGold,
                Margin            = new Thickness(0, 0, 0, 2),
            });

            foreach (var offering in state.Offerings)
            {
                var capturedTarget = offering.Target;

                var btn = new Button { HorizontalExpand = true };
                btn.Label.Text              = Loc.GetString("heretic-offering-entry",
                    ("name", offering.Name), ("kp", offering.KnowledgeGain));
                btn.Label.FontColorOverride = OfferingRed;
                btn.OnPressed += _ =>
                {
                    OnOfferingSelected?.Invoke(capturedTarget);
                    Close();
                };
                _list.AddChild(btn);
            }
        }
    }

    private sealed class GradientPanel : PanelContainer
    {
        private static readonly Color GradTop    = Color.FromHex("#1e0d30");
        private static readonly Color GradBottom = Color.FromHex("#06020e");
        private static readonly Color Border     = Color.FromHex("#5c2888");

        public GradientPanel(int marginH = 8, int marginV = 6)
        {
            HorizontalExpand = true;
            VerticalExpand   = true;
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor             = Color.Transparent,
                ContentMarginLeftOverride   = marginH,
                ContentMarginRightOverride  = marginH,
                ContentMarginTopOverride    = marginV,
                ContentMarginBottomOverride = marginV,
            };
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var w = Size.X;
            var h = Size.Y;
            if (w <= 0 || h <= 0)
                return;

            const int Steps = 64;
            var stepH = h / Steps;
            for (var i = 0; i < Steps; i++)
            {
                var t = (float)i / Steps;
                var r = GradTop.R + (GradBottom.R - GradTop.R) * t;
                var g = GradTop.G + (GradBottom.G - GradTop.G) * t;
                var b = GradTop.B + (GradBottom.B - GradTop.B) * t;
                handle.DrawRect(new UIBox2(0, i * stepH, w, (i + 1) * stepH), new Color(r, g, b));
            }

            handle.DrawRect(new UIBox2(0, 0, w, h), Border, filled: false);
        }
    }
}
