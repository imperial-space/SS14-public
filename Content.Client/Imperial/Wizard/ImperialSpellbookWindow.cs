using Content.Shared.Imperial.Helpers;
using Content.Shared.Dataset;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Wizard;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Imperial.Wizard;

/// <summary>
/// tgui Spellbook: раскрытая книга из двух страниц (Оглавление, Наступление, Защита, Мобильность, Поддержка,
/// Испытания, Ритуалы, Комплекты, Случайно, Перки), полоса очков и поиск.
/// </summary>
public sealed class ImperialSpellbookWindow : DefaultWindow
{
    public event Action<string>? OnPurchase;
    public event Action<string>? OnRefund;
    public event Action<bool>? OnRandomize;
    public event Action<string>? OnLoadout;

    /// <summary>theme-wizard: тёмно-синие страницы с фиолетовым.</summary>
    public static readonly TguiTheme Theme = new()
    {
        BaseStart = Color.FromHex("#2c2f5e"),
        BaseEnd = Color.FromHex("#1b1d3d"),
        Section = Color.FromHex("#14162e"),
        Primary = Color.FromHex("#4b3f8c"),
        SectionLine = Color.FromHex("#6e5ec4"),
        Label = Color.FromHex("#a49bd8"),
        Notice = Color.FromHex("#5b4a9c"),
        NoticeText = Color.White,
        Selected = Color.FromHex("#7a5cd6"),
        Danger = Color.FromHex("#d92626"),
        Header = Color.FromHex("#14162e"),
    };

    private static readonly ProtoId<LocalizedDatasetPrototype> SearchVerbsDataset = "ImperialImperialSpellbookWindowSearchVerbs";

    private static string[] SearchVerbs => ImperialLocalizedLists.Get(SearchVerbsDataset);

    /// <summary>Страницы TAB2NAME.</summary>
    private enum Page
    {
        EnscribedName,
        TableOfContents,
        Offensive,
        Defensive,
        Mobility,
        Assistance,
        Challenges,
        Rituals,
        Loadouts,
        Randomize,
        Perks,
        TableOfContents2,
    }

    private readonly IPrototypeManager _proto;
    private readonly IComponentFactory _factory;
    private readonly SpriteSystem _sprite;
    private readonly BoxContainer _pages = Tgui.HBox(8);
    private readonly LineEdit _search = new() { MinWidth = 150 };
    private readonly Control _points = new() { HorizontalExpand = true };
    private ImperialSpellbookState? _state;
    private int _tab = 1;
    private string _verb = SearchVerbs[0];

    public ImperialSpellbookWindow()
    {
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _factory = IoCManager.Resolve<IComponentFactory>();
        _sprite = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        Title = Loc.GetString("imperial-spellbook-title");
        MinSize = SetSize = new Vector2(950, 560);

        _search.PlaceHolder = Loc.GetString("imperial-spellbook-search");
        _search.OnTextChanged += _ =>
        {
            if (_search.Text.Length == 0)
                _verb = IoCManager.Resolve<IRobustRandom>().Pick(SearchVerbs);
            Rebuild();
        };

        _pages.VerticalExpand = true;
        var bottom = Tgui.MakeSection(null, null, Tgui.HBox(6, _points, _search), Theme);
        var root = Tgui.VBox(6, _pages, bottom);
        root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(root, Theme, scrollable: false));
    }

    public void Update(ImperialSpellbookState state)
    {
        _state = state;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state is not { } state)
            return;

        _points.RemoveAllChildren();
        var bar = Tgui.ProgressBar(Math.Clamp(state.Points / (float)state.MaxPoints, 0f, 1f), Theme.Primary,
            Loc.GetString("imperial-spellbook-points", ("points", state.Points)));
        bar.HorizontalExpand = true;
        _points.AddChild(bar);

        _pages.RemoveAllChildren();
        var search = _search.Text.Trim();
        if (search.Length > 1)
        {
            var results = state.Entries.Where(e => _proto.TryIndex<ImperialSpellbookEntryPrototype>(e.Id, out var entry)
                    && (e.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || Loc.GetString(entry.Desc).Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var stop = WizardIcons.Button(Loc.GetString("imperial-spellbook-stop-search", ("verb", _verb)), "arrow-rotate-left",
                () => _search.Text = string.Empty, theme: Theme);
            var page = MakePage($"{_verb}...", stop, EntryList(results, state), null);
            page.HorizontalExpand = true;
            _pages.AddChild(page);
            return;
        }

        var left = (Page)(_tab - 1);
        var right = (Page)_tab;

        var prev = WizardIcons.Button(Loc.GetString("imperial-spellbook-prev"), "arrow-left", () =>
        {
            _tab -= 2;
            Rebuild();
        }, disabled: _tab == 1, theme: Theme);
        var next = WizardIcons.Button(Loc.GetString("imperial-spellbook-next"), "arrow-right", () =>
        {
            _tab += 2;
            Rebuild();
        }, disabled: _tab == 11, theme: Theme);

        _pages.AddChild(MakePage(PageTitle(left), Tgui.HBox(8, prev, Tgui.Text($"{_tab}", bold: true)), PageContent(left, state), left));
        _pages.AddChild(MakePage(PageTitle(right), Tgui.HBox(8, Tgui.Text($"{_tab + 1}", bold: true), next), PageContent(right, state), right));
    }

    private Control MakePage(string title, Control buttons, Control content, Page? page)
    {
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(content);
        var section = Tgui.MakeSection(title, buttons, scroll, Theme);
        section.MinWidth = 466;
        section.HorizontalExpand = true;
        section.VerticalExpand = true;
        return section;
    }

    private static string PageTitle(Page page)
    {
        return Loc.GetString($"imperial-spellbook-page-{page.ToString().ToLowerInvariant()}");
    }

    private Control PageContent(Page page, ImperialSpellbookState state)
    {
        var box = Tgui.VBox(6);
        var blurb = $"imperial-spellbook-blurb-{page.ToString().ToLowerInvariant()}";
        if (Loc.TryGetString(blurb, out var blurbText))
        {
            var label = Tgui.Paragraph($"[bold]{blurbText}[/bold]");
            box.AddChild(label);
        }

        switch (page)
        {
            case Page.EnscribedName:
                var name = Tgui.Text(state.Owner, Tgui.Bad, 36, align: HAlignment.Center, italic: true);
                name.Margin = new Thickness(0, 120, 0, 0);
                box.AddChild(name);
                break;
            case Page.TableOfContents:
            case Page.TableOfContents2:
                box.AddChild(TableOfContents());
                break;
            case Page.Loadouts:
                box.AddChild(Locked(state.Points < state.MaxPoints, Loadouts(), "dollar-sign", "imperial-spellbook-point-locked"));
                break;
            case Page.Randomize:
                box.AddChild(Locked(state.Points < state.MaxPoints, Randomize(state), "dollar-sign", "imperial-spellbook-point-locked"));
                break;
            case Page.Challenges:
                box.AddChild(Locked(true, EntryList(Entries(state, ImperialSpellbookCategory.Challenges), state), "lock",
                    "imperial-spellbook-federation-locked"));
                break;
            default:
                var category = page switch
                {
                    Page.Offensive => ImperialSpellbookCategory.Offensive,
                    Page.Defensive => ImperialSpellbookCategory.Defensive,
                    Page.Mobility => ImperialSpellbookCategory.Mobility,
                    Page.Assistance => ImperialSpellbookCategory.Assistance,
                    Page.Rituals => ImperialSpellbookCategory.Rituals,
                    _ => ImperialSpellbookCategory.Perks,
                };
                box.AddChild(EntryList(Entries(state, category), state));
                break;
        }

        return box;
    }

    private List<ImperialSpellbookEntryState> Entries(ImperialSpellbookState state, ImperialSpellbookCategory category)
    {
        return state.Entries.Where(e => _proto.TryIndex<ImperialSpellbookEntryPrototype>(e.Id, out var entry) && entry.Category == category)
            .ToList();
    }

    /// <summary>Dimmer поверх страницы: «Федерация заблокировала» или «недостаточно очков».</summary>
    private static Control Locked(bool locked, Control content, string icon, string text)
    {
        if (!locked)
            return content;

        var layers = new LayoutContainer { HorizontalExpand = true, VerticalExpand = true };
        layers.AddChild(content);
        var dimmer = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.75f) },
            MouseFilter = MouseFilterMode.Stop,
        };
        var message = Tgui.VBox(10, WizardIcons.Icon(icon, 96, Color.FromHex("#a74fff")),
            Tgui.Text(Loc.GetString(text), Color.FromHex("#a74fff"), 18, align: HAlignment.Center));
        message.HorizontalAlignment = HAlignment.Center;
        message.VerticalAlignment = VAlignment.Center;
        foreach (var child in message.Children)
            child.HorizontalAlignment = HAlignment.Center;
        dimmer.AddChild(message);
        layers.AddChild(dimmer);
        LayoutContainer.SetAnchorPreset(dimmer, LayoutContainer.LayoutPreset.Wide);
        layers.MinHeight = 380;
        return layers;
    }

    private Control TableOfContents()
    {
        var box = Tgui.VBox(4);
        TguiButton Item(string text, string icon, Page? page)
        {
            var button = WizardIcons.Button(Loc.GetString(text), icon, page == null ? null : () =>
            {
                _tab = (int)page.Value % 2 == 0 ? (int)page.Value + 1 : (int)page.Value;
                Rebuild();
            }, disabled: page == null, fluid: true, theme: Theme);
            button.MinHeight = 30;
            return button;
        }

        // Как в TG: кнопки ведут на страницу защиты/поддержки/ритуалов/случайного — разворот, где лежит нужное.
        box.AddChild(Item("imperial-spellbook-toc-rename", "pen", null));
        box.AddChild(Item("imperial-spellbook-toc-contents", "clipboard", null));
        box.AddChild(Divider());
        box.AddChild(Item("imperial-spellbook-toc-deadly", "fire", Page.Defensive));
        box.AddChild(Item("imperial-spellbook-toc-defensive", "shield-alt", Page.Defensive));
        box.AddChild(Divider());
        box.AddChild(Item("imperial-spellbook-toc-travel", "globe-americas", Page.Assistance));
        box.AddChild(Item("imperial-spellbook-toc-assistance", "users", Page.Assistance));
        box.AddChild(Divider());
        box.AddChild(Item("imperial-spellbook-toc-challenges", "crown", Page.Rituals));
        box.AddChild(Item("imperial-spellbook-toc-rituals", "magic", Page.Rituals));
        box.AddChild(Divider());
        box.AddChild(Item("imperial-spellbook-toc-loadouts", "thumbs-up", Page.Randomize));
        box.AddChild(Item("imperial-spellbook-toc-random", "dice", Page.Randomize));
        box.AddChild(Divider());
        box.AddChild(Item("imperial-spellbook-toc-perks", "cog", Page.TableOfContents2));
        return box;
    }

    private static Control Divider()
    {
        return new PanelContainer
        {
            MinHeight = 2,
            Margin = new Thickness(0, 4),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.White.WithAlpha(0.1f) },
        };
    }

    /// <summary>SpellTabDisplay: записи по алфавиту — откат, цена, мантия, описание, «Выучить» и «Вернуть».</summary>
    private Control EntryList(List<ImperialSpellbookEntryState> entries, ImperialSpellbookState state)
    {
        var box = Tgui.VBox(4);
        foreach (var entryState in entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!_proto.TryIndex<ImperialSpellbookEntryPrototype>(entryState.Id, out var entry))
                continue;

            box.AddChild(Divider());

            var info = Tgui.HBox(8);
            string timeOrCat;
            if (entry.Category == ImperialSpellbookCategory.Rituals)
                timeOrCat = entryState.Times > 0
                    ? Loc.GetString("imperial-spellbook-cast-times", ("times", entryState.Times))
                    : Loc.GetString("imperial-spellbook-not-cast");
            else
                timeOrCat = entryState.Cooldown is { } cd && cd > 0
                    ? Loc.GetString("imperial-spellbook-cooldown", ("seconds", MathF.Round(cd, 1)))
                    : string.Empty;

            var timeLabel = Tgui.Text(timeOrCat, Theme.Label);
            timeLabel.MinWidth = 120;
            info.AddChild(timeLabel);
            var costLabel = Tgui.Text(Loc.GetString("imperial-spellbook-cost", ("cost", entry.Cost)), Theme.Label);
            costLabel.MinWidth = 60;
            info.AddChild(costLabel);
            if (entry.BuyWord == ImperialSpellbookBuyWord.Learn)
            {
                var garb = WizardIcons.Button(string.Empty, "tshirt", null, entryState.RequiresGarb ? Tgui.Bad : Tgui.Green,
                    tooltip: Loc.GetString(entryState.RequiresGarb ? "imperial-spellbook-garb-required" : "imperial-spellbook-garb-free"));
                info.AddChild(garb);
            }

            box.AddChild(info);

            var id = entry.ID;
            var buyIcon = entry.BuyWord switch
            {
                ImperialSpellbookBuyWord.Summon => "hat-wizard",
                ImperialSpellbookBuyWord.Cast => "meteor",
                _ => "plus",
            };
            var buy = WizardIcons.Button(Loc.GetString($"imperial-spellbook-buyword-{entry.BuyWord.ToString().ToLowerInvariant()}"),
                buyIcon, () => OnPurchase?.Invoke(id), state.Points >= entry.Cost ? Tgui.Green : Tgui.Bad,
                disabled: state.Points < entry.Cost);
            buy.MinWidth = 105;

            var refund = entry.Refundable
                ? WizardIcons.Button(Loc.GetString("imperial-spellbook-refund"), "arrow-left", () => OnRefund?.Invoke(id), theme: Theme)
                : Tgui.NoticeBox(Loc.GetString("imperial-spellbook-no-refund-notice"), theme: Theme);
            refund.MinWidth = 105;

            var buttons = Tgui.VBox(4, buy, refund);
            var desc = Tgui.Paragraph(Loc.GetString(entry.Desc));
            desc.HorizontalExpand = true;
            var body = Tgui.HBox(8, EntryIcon(entry), desc, buttons);
            box.AddChild(Tgui.MakeSection(entryState.Name, null, body, Theme));
        }

        if (entries.Count == 0)
            box.AddChild(Tgui.Text(Loc.GetString("imperial-spellbook-empty"), Theme.Label, align: HAlignment.Center));

        return box;
    }

    /// <summary>Иконка записи: значок заклинания, первый предмет или iconEntity.</summary>
    private Control EntryIcon(ImperialSpellbookEntryPrototype entry)
    {
        var frame = new PanelContainer
        {
            MinSize = new Vector2(52, 52),
            MaxSize = new Vector2(52, 52),
            VerticalAlignment = VAlignment.Top,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Theme.Primary.WithAlpha(0.35f),
                BorderColor = Theme.SectionLine,
                BorderThickness = new Thickness(1),
            },
        };

        if (entry.Spell is { } spell && _proto.TryIndex(spell, out var spellProto)
            && spellProto.TryGetComponent<ActionComponent>(out var action, _factory) && action.Icon is { } icon)
        {
            frame.AddChild(new TextureRect
            {
                Texture = _sprite.Frame0(icon),
                TextureScale = new Vector2(1.5f, 1.5f),
                Stretch = TextureRect.StretchMode.KeepCentered,
            });
            return frame;
        }

        var entity = entry.Items.Count > 0 ? entry.Items[0] : entry.IconEntity;
        if (entity != null)
        {
            var view = new EntityPrototypeView { Scale = new Vector2(1.5f, 1.5f), HorizontalAlignment = HAlignment.Center, VerticalAlignment = VAlignment.Center };
            view.SetPrototype(entity);
            frame.AddChild(view);
            return frame;
        }

        var glyph = WizardIcons.Icon("magic", 24, Theme.Label);
        glyph.HorizontalAlignment = HAlignment.Center;
        frame.AddChild(glyph);
        return frame;
    }

    /// <summary>Loadouts.tsx: четыре одобренных набора.</summary>
    private Control Loadouts()
    {
        Control Single(string id, string icon, Color color)
        {
            var body = Tgui.VBox(6,
                Tgui.Paragraph(Loc.GetString($"imperial-spellbook-{id}-blurb")),
                Divider(),
                WizardIcons.Confirm(Loc.GetString("imperial-spellbook-loadout-buy"), icon, Loc.GetString("imperial-spellbook-loadout-confirm"),
                    () => OnLoadout?.Invoke(id), theme: Theme),
                Divider(),
                Tgui.Text(Loc.GetString($"imperial-spellbook-{id}-author"), color));
            var section = Tgui.MakeSection(Loc.GetString($"imperial-spellbook-{id}-name"), null, body, Theme);
            section.HorizontalExpand = true;
            return section;
        }

        // Страница узкая — наборы друг под другом, иначе правый сжимается в столбик.
        return Tgui.VBox(6,
            Single("loadout_classic", "fire", Color.FromHex("#a74fff")),
            Single("loadout_hammer", "hammer", Tgui.Green),
            Single("loadout_army", "pastafarianism", Tgui.Yellow),
            Single("loadout_tap", "skull", Color.White));
    }

    /// <summary>Randomize.tsx.</summary>
    private Control Randomize(ImperialSpellbookState state)
    {
        var semi = WizardIcons.Confirm(Loc.GetString("imperial-spellbook-semirandom"), "dice-three", Loc.GetString("imperial-spellbook-random-confirm"),
            () => OnRandomize?.Invoke(false), theme: Theme, height: 60);
        var full = WizardIcons.Confirm(Loc.GetString("imperial-spellbook-fullrandom"), "dice", Loc.GetString("imperial-spellbook-random-confirm"),
            () => OnRandomize?.Invoke(true), Color.Black, Theme, 60);
        return Tgui.VBox(8,
            Tgui.Paragraph(Loc.GetString("imperial-spellbook-semirandom-desc", ("points", state.SemiRandom))),
            semi,
            Divider(),
            Tgui.Paragraph(Loc.GetString("imperial-spellbook-fullrandom-desc", ("points", state.FullRandom))),
            full);
    }
}
