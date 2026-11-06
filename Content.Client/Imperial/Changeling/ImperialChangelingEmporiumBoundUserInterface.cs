using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Shared.Imperial.Changeling;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Changeling;

/// <summary>
/// tgui CellularEmporium.tsx: шапка с ДНК и переадаптацией, слева поиск, компактный вид и вкладки,
/// справа карточки способностей с ценой.
/// </summary>
[UsedImplicitly]
public sealed class ImperialChangelingEmporiumBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ResPath Actions = new("/Textures/Imperial/Changeling/actions.rsi");

    /// <summary>CATEGORY_ORDER = ['combat', 'stealth', 'utility', 'stings'].</summary>
    private static readonly ImperialChangelingCategory[] CategoryOrder =
    {
        ImperialChangelingCategory.Combat,
        ImperialChangelingCategory.Stealth,
        ImperialChangelingCategory.Utility,
        ImperialChangelingCategory.Stings,
    };

    private DefaultWindow? _window;
    private RichTextLabel _dna = default!;
    private Button _readapt = default!;
    private LineEdit _search = default!;
    private Button _compactButton = default!;
    private BoxContainer _tabs = default!;
    private BoxContainer _list = default!;
    private ImperialChangelingCategory _category = ImperialChangelingCategory.Combat;
    private bool _compact;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-changeling-emporium-title");
        _window.SetSize = new Vector2(900, 520);
        Build(_window);
        Update();
    }

    private void Build(DefaultWindow window)
    {
        var background = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = TguiStyle.WindowBackground },
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        window.Contents.AddChild(background);
        background.AddChild(TguiStyle.Watermark(IoCManager.Resolve<Robust.Client.ResourceManagement.IResourceCache>().GetResource<Robust.Client.ResourceManagement.TextureResource>("/Textures/Interface/Nano/ntlogo.svg.png").Texture));

        var section = TguiStyle.Section(out var titleRow, out var body);
        section.Margin = new Thickness(4);
        background.AddChild(section);

        // <Icon name="dna" /> {genetic_points_count} DNA
        titleRow.AddChild(TguiStyle.Icon(EntMan.System<SpriteSystem>().Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Changeling/hud.rsi"), "extract_dna_sting")), 24));
        _dna = new RichTextLabel { VerticalAlignment = Control.VAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        titleRow.AddChild(_dna);
        titleRow.AddChild(new Control { HorizontalExpand = true });
        _readapt = new Button();
        _readapt.OnPressed += _ => SendMessage(new ImperialChangelingReadaptMessage());
        titleRow.AddChild(_readapt);

        var columns = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, VerticalExpand = true, SeparationOverride = 8 };
        body.AddChild(columns);

        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 180, MaxWidth = 180, SeparationOverride = 4 };
        _search = new LineEdit { PlaceHolder = Loc.GetString("imperial-changeling-emporium-search") };
        _search.OnTextChanged += _ => Update();
        left.AddChild(_search);

        _compactButton = new Button { MinHeight = 32 };
        _compactButton.OnPressed += _ =>
        {
            _compact = !_compact;
            Update();
        };
        left.AddChild(_compactButton);

        _tabs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(0, 4, 0, 0) };
        left.AddChild(_tabs);
        columns.AddChild(left);

        var scroll = new ScrollContainer { HScrollEnabled = false, HorizontalExpand = true, VerticalExpand = true };
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 8 };
        scroll.AddChild(_list);
        columns.AddChild(scroll);
    }

    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent<ImperialChangelingComponent>(Owner, out var ling))
            return;

        _dna.SetMarkup($"[color=#DD66DD][font size=16]{ling.GeneticPoints} {Loc.GetString("imperial-changeling-emporium-dna")}[/font][/color]");
        _readapt.Text = Loc.GetString("imperial-changeling-emporium-readapt-count", ("count", ling.CanRespec));
        _readapt.Disabled = ling.CanRespec <= 0;
        _readapt.ToolTip = Loc.GetString(ling.CanRespec > 0 ? "imperial-changeling-emporium-readapt-tip" : "imperial-changeling-emporium-readapt-tip-none");
        TguiStyle.Paint(_readapt, TguiStyle.Good);

        _compactButton.Text = Loc.GetString(_compact ? "imperial-changeling-emporium-detailed" : "imperial-changeling-emporium-compact");
        _compactButton.ToolTip = _compactButton.Text;

        _tabs.RemoveAllChildren();
        foreach (var category in CategoryOrder)
        {
            var tab = new Button
            {
                Text = Loc.GetString($"imperial-changeling-category-{category.ToString().ToLowerInvariant()}"),
                ToggleMode = true,
                Pressed = category == _category,
                MinHeight = 28,
            };
            var captured = category;
            tab.OnPressed += _ =>
            {
                _category = captured;
                _search.Text = string.Empty;
                Update();
            };
            TguiStyle.Paint(tab, category == _category ? TguiStyle.TabSelected : TguiStyle.TabNormal);
            _tabs.AddChild(tab);
        }

        var search = _search.Text.Trim();
        var abilities = _proto.EnumeratePrototypes<ImperialChangelingPowerPrototype>()
            .Where(p => p.DnaCost >= 0 && !p.Innate)
            .Select(p => (Power: p, Name: Loc.GetString(p.Name), Desc: Loc.GetString(p.Description), Help: p.HelpText is { } h ? Loc.GetString(h) : string.Empty))
            .Where(p => search.Length > 0
                ? $"{p.Name} {p.Desc} {p.Help}".Contains(search, StringComparison.CurrentCultureIgnoreCase)
                : p.Power.Category == _category)
            .OrderBy(p => p.Name)
            .ToList();

        _list.RemoveAllChildren();
        if (abilities.Count == 0)
        {
            _list.AddChild(TguiStyle.NoticeBox(Loc.GetString(search.Length > 0
                ? "imperial-changeling-emporium-none-found"
                : "imperial-changeling-emporium-none-category")));
            return;
        }

        var sprite = EntMan.System<SpriteSystem>();
        var compact = search.Length > 0 || _compact;
        foreach (var (power, name, desc, help) in abilities)
        {
            _list.AddChild(compact ? MakeCompact(power, name, desc, help, ling, sprite) : MakeDetailed(power, name, desc, help, ling, sprite));
        }
    }

    private static Texture Texture(string path)
    {
        return IoCManager.Resolve<Robust.Client.ResourceManagement.IResourceCache>().GetResource<Robust.Client.ResourceManagement.TextureResource>(path).Texture;
    }

    private Control MakeIcon(ImperialChangelingPowerPrototype power, SpriteSystem sprite, float size)
    {
        // DmIcon bg_changeling + DmIcon icon_state, наложенные друг на друга.
        var icon = new LayoutContainer { SetSize = new Vector2(size, size), Margin = new Thickness(16, 0, 8, 0), VerticalAlignment = Control.VAlignment.Top };
        foreach (var state in new[] { "bg_changeling", power.Icon })
        {
            var rect = new TextureRect
            {
                Texture = sprite.Frame0(new SpriteSpecifier.Rsi(Actions, state)),
                TextureScale = new Vector2(size / 32, size / 32),
                Stretch = TextureRect.StretchMode.Scale,
                SetSize = new Vector2(size, size),
            };
            icon.AddChild(rect);
        }

        return icon;
    }

    private Button MakeCostButton(ImperialChangelingPowerPrototype power, ImperialChangelingComponent ling, bool owned, bool canAfford)
    {
        // costDisplay: dna_required, если есть, иначе genetic_point_required.
        var cost = power.ReqDna > 0 ? power.ReqDna : power.DnaCost;
        var button = new Button
        {
            Text = Loc.GetString("imperial-changeling-emporium-cost", ("cost", cost)),
            Disabled = owned || !canAfford,
            VerticalAlignment = Control.VAlignment.Center,
            ToolTip = Requirements(power),
        };
        TguiStyle.Paint(button, owned ? TguiStyle.Good : canAfford ? TguiStyle.Average : TguiStyle.Bad);
        button.OnPressed += _ => SendMessage(new ImperialChangelingEvolveMessage(power.ID));
        return button;
    }

    /// <summary>requirementTooltip: «N DNA, M absorptions».</summary>
    private static string Requirements(ImperialChangelingPowerPrototype power)
    {
        var parts = new List<string> { Loc.GetString("imperial-changeling-emporium-req-points", ("count", power.DnaCost)) };
        if (power.ReqAbsorbs > 0)
            parts.Add(Loc.GetString("imperial-changeling-emporium-req-absorbs", ("count", power.ReqAbsorbs)));
        if (power.ReqDna > 0)
            parts.Add(Loc.GetString("imperial-changeling-emporium-req-dna", ("count", power.ReqDna)));
        return string.Join(", ", parts);
    }

    private static bool CanAfford(ImperialChangelingPowerPrototype power, ImperialChangelingComponent ling, bool owned)
    {
        return !owned && power.DnaCost <= ling.GeneticPoints && power.ReqAbsorbs <= ling.TrueAbsorbs && power.ReqDna <= ling.AbsorbedCount;
    }

    private Control MakeDetailed(ImperialChangelingPowerPrototype power, string name, string desc, string help, ImperialChangelingComponent ling, SpriteSystem sprite)
    {
        var owned = ling.Purchased.Contains(power.ID);
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        row.AddChild(MakeIcon(power, sprite, 64));

        var section = TguiStyle.Section(out var titleRow, out var body);
        section.HorizontalExpand = true;
        var title = new RichTextLabel { VerticalAlignment = Control.VAlignment.Center };
        var ownedText = owned ? $" [color=#44bd46]{Loc.GetString("imperial-changeling-emporium-owned")}[/color]" : string.Empty;
        title.SetMarkup($"[bold]{FormattedMessage.EscapeText(name)}[/bold]{ownedText}");
        titleRow.AddChild(title);
        titleRow.AddChild(new Control { HorizontalExpand = true });
        titleRow.AddChild(MakeCostButton(power, ling, owned, CanAfford(power, ling, owned)));

        var descLabel = new RichTextLabel { Modulate = Color.White.WithAlpha(0.8f) };
        descLabel.SetMessage(desc);
        body.AddChild(descLabel);
        if (help.Length > 0)
        {
            var helpLabel = new RichTextLabel { Margin = new Thickness(0, 4, 0, 0) };
            helpLabel.SetMarkup($"[color=#44bd46]{FormattedMessage.EscapeText(help)}[/color]");
            body.AddChild(helpLabel);
        }

        row.AddChild(section);
        return row;
    }

    private Control MakeCompact(ImperialChangelingPowerPrototype power, string name, string desc, string help, ImperialChangelingComponent ling, SpriteSystem sprite)
    {
        var owned = ling.Purchased.Contains(power.ID);
        var panel = new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = TguiStyle.SectionBackground } };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 2) };
        panel.AddChild(row);
        row.AddChild(MakeIcon(power, sprite, 32));

        var label = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = Control.VAlignment.Center, Modulate = owned ? Color.White.WithAlpha(0.5f) : Color.White };
        if (owned)
            row.AddChild(TguiStyle.Icon(Texture("/Textures/Interface/Nano/checkbox_checked.svg.96dpi.png"), 16));
        label.SetMarkup(owned
            ? $"[color=#44bd46][bold]{FormattedMessage.EscapeText(name)}[/bold][/color]"
            : $"[bold]{FormattedMessage.EscapeText(name)}[/bold]");
        row.AddChild(label);

        // <Icon name="info-circle" /> с подсказкой helptext || desc.
        var info = TguiStyle.Icon(Texture("/Textures/Interface/info.svg.192dpi.png"), 16, help.Length > 0 ? help : desc);
        info.Margin = new Thickness(4, 0);
        row.AddChild(info);

        var button = MakeCostButton(power, ling, owned, CanAfford(power, ling, owned));
        button.Margin = new Thickness(8);
        row.AddChild(button);
        return panel;
    }
}
