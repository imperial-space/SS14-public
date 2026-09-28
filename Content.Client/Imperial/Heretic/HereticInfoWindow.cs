using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Imperial.Heretic;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticInfoWindow : DefaultWindow
{
    public event Action<string>? OnPathSelected;
    public event Action<string>? OnKnowledgeSelected;
    public event Action? OnDenyAscension;

    private static readonly Color BrightGreen = Color.FromHex("#55cc55");
    private static readonly Color DimGreen = Color.FromHex("#88aa88");
    private static readonly Color LockGray = Color.FromHex("#555555");
    private static readonly Color HeaderGold = Color.FromHex("#e0c070");


    private readonly SpriteSystem _spriteSystem;

    // ─── Tab 2: path info ───────────────────────────────────────────────────
    private readonly ScrollContainer _leftScroll;
    private readonly BoxContainer _pathList;

    // Selection panel (CurrentPath == General — path not yet chosen)
    private readonly ScrollContainer _selectionScroll;
    private readonly TextureRect _pathIconRect;
    private readonly TextureRect _bladeIconRect;
    private readonly Label _pathNameLabel;
    private readonly Label _complexityLabel;
    private readonly RichTextLabel _descLabel;
    private readonly Label _passiveHeaderLabel;
    private readonly Label _passiveNameLabel;
    private readonly RichTextLabel _passiveDescLabel;
    private readonly RichTextLabel _prosLabel;
    private readonly RichTextLabel _consLabel;
    private readonly Label _guaranteedHeader;
    private readonly BoxContainer _guaranteedRow;
    private readonly PanelContainer _guaranteedBg;

    // Selected panel (CurrentPath != General — path already chosen)
    private readonly ScrollContainer _selectedScroll;
    private readonly Label _selectedPathNameLabel;
    private readonly RichTextLabel _selectedDescLabel;
    private readonly Label _passiveAbilityLabel;
    private readonly Label _tipsHeader;
    private readonly RichTextLabel _tipsLabel;

    // Level progress boxes (horizontal row in selected panel)
    private readonly BoxContainer _passiveProgressBox;
    private readonly PanelContainer _level1Panel;
    private readonly PanelContainer _level2Panel;
    private readonly PanelContainer _level3Panel;
    private readonly Label _level1TitleLabel;
    private readonly Label _level2TitleLabel;
    private readonly Label _level3TitleLabel;
    private readonly RichTextLabel _level1DescLabel;
    private readonly RichTextLabel _level2DescLabel;
    private readonly RichTextLabel _level3DescLabel;

    // ─── Tab 1: info ─────────────────────────────────────────────────────────
    private readonly RichTextLabel _infoKpLabel;
    private readonly Label _infoSacrificeLabel;
    private readonly BoxContainer _infoTasksBox;
    private readonly Button _denyAscensionButton;

    // ─── Tab 3: research ────────────────────────────────────────────────────
    private readonly RichTextLabel _pointsLabel;
    private readonly KnowledgeGraphControl _treeBox;
    private readonly BoxContainer _ascensionPanel;
    private readonly BoxContainer _shopPanel;
    private readonly ScrollContainer _shopScroll;

    private HereticInfoBuiState? _state;
    private string? _selectedKnowledgeId;
    private Button? _activeButton;

    public HereticInfoWindow()
    {
        _spriteSystem = IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();

        Title = Loc.GetString("heretic-info-window-title");
        MinSize = new Vector2(720, 700);

        // Style the outer window panel (grey frame → dark purple)
        foreach (var child in Children)
        {
            if (child is PanelContainer wp && wp.StyleClasses.Contains(StyleClassWindowPanel))
            {
                wp.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#0a0118") };
                break;
            }
        }

        // Title bar: dark purple background, light purple text
        WindowHeader.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1a0830") };
        TitleLabel.FontColorOverride = Color.FromHex("#d4a4ff");

        var tabs = new TabContainer { HorizontalExpand = true, VerticalExpand = true };
        tabs.TabFontColorOverride = Color.FromHex("#55aaff");
        tabs.TabFontColorInactiveOverride = Color.FromHex("#3366bb");

        // ─── Tab 1: info ─────────────────────────────────────────────────────
        var tab1Root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 2,
            HorizontalExpand = true,
        };

        tab1Root.AddChild(new Label
        {
            Text = Loc.GetString("heretic-info-you-are-heretic"),
            FontColorOverride = HeaderGold,
        });
        tab1Root.AddChild(MakeSeparator());

        var guideText = new RichTextLabel { HorizontalExpand = true };
        guideText.Text = Loc.GetString("heretic-info-guide-text");
        tab1Root.AddChild(guideText);

        tab1Root.AddChild(MakeSeparator());

        _infoKpLabel = new RichTextLabel { HorizontalExpand = true };
        _infoSacrificeLabel = new Label { FontColorOverride = Color.White };
        tab1Root.AddChild(_infoKpLabel);
        tab1Root.AddChild(_infoSacrificeLabel);

        tab1Root.AddChild(MakeSeparator());

        tab1Root.AddChild(new Label
        {
            Text = Loc.GetString("heretic-info-tasks-header"),
            FontColorOverride = HeaderGold,
        });

        _infoTasksBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalExpand = true,
        };
        tab1Root.AddChild(_infoTasksBox);

        _denyAscensionButton = new Button
        {
            Text = Loc.GetString("heretic-info-deny-ascension"),
            StyleClasses = { StyleNano.ButtonCaution },
        };
        _denyAscensionButton.OnPressed += _ => OnDenyAscension?.Invoke();
        var denyRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        denyRow.AddChild(_denyAscensionButton);
        denyRow.AddChild(new Control { HorizontalExpand = true });
        tab1Root.AddChild(denyRow);

        var tab1Scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        tab1Scroll.AddChild(tab1Root);
        var tab1Panel = new GradientPanel(0, 0);
        tab1Panel.AddChild(tab1Scroll);
        tabs.AddChild(tab1Panel);

        // ─── Tab 2: path info ────────────────────────────────────────────────
        var tab2Root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(6),
            SeparationOverride = 8,
        };

        // Left: path-selection list (visible only when CurrentPath == General)
        _leftScroll = new ScrollContainer
        {
            MinWidth = 165,
            HorizontalExpand = false,
            VerticalExpand = true,
        };

        _pathList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        _leftScroll.AddChild(_pathList);

        // ─── Selection panel (CurrentPath == General) ────────────────────────
        _selectionScroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };

        var selectionPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            HorizontalExpand = true,
        };

        _pathNameLabel = new Label
        {
            Text = Loc.GetString("heretic-path-select-placeholder"),
            FontColorOverride = HeaderGold,
            HorizontalExpand = true,
            Align = Label.AlignMode.Center,
        };

        var _cache = IoCManager.Resolve<IResourceCache>();
        var _fontRes = _cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf");
        var _bigFont = new VectorFont(_fontRes, 16);

        var choosePathLabelText = new Label
        {
            Text = Loc.GetString("heretic-info-choose-path"),
            FontColorOverride = HeaderGold,
            FontOverride = _bigFont,
            HorizontalExpand = true,
            Align = Label.AlignMode.Center,
        };
        var choosePathUnderline = new PanelContainer
        {
            HorizontalExpand = true,
            MinSize = new Vector2(0, 2),
            Margin = new Thickness(30, 0, 30, 0),
            PanelOverride = new StyleBoxFlat { BackgroundColor = HeaderGold },
        };
        var choosePathLabel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };
        choosePathLabel.AddChild(choosePathLabelText);
        choosePathLabel.AddChild(choosePathUnderline);

        var iconCenterRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        _pathIconRect = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
        };
        _bladeIconRect = new TextureRect
        {
            MinSize = new Vector2(40, 40),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            Visible = false,
        };
        var iconContainer = new LayoutContainer
        {
            MinSize = new Vector2(80, 80),
            InheritChildMeasure = false,
        };
        LayoutContainer.SetAnchorAndMarginPreset(_pathIconRect, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorLeft(_bladeIconRect, 0.5f);
        LayoutContainer.SetAnchorTop(_bladeIconRect, 0.5f);
        LayoutContainer.SetAnchorRight(_bladeIconRect, 0.5f);
        LayoutContainer.SetAnchorBottom(_bladeIconRect, 0.5f);
        LayoutContainer.SetMarginLeft(_bladeIconRect, -20f);
        LayoutContainer.SetMarginTop(_bladeIconRect, -20f);
        LayoutContainer.SetMarginRight(_bladeIconRect, 20f);
        LayoutContainer.SetMarginBottom(_bladeIconRect, 20f);
        iconContainer.AddChild(_pathIconRect);
        iconContainer.AddChild(_bladeIconRect);
        iconCenterRow.AddChild(new Control { HorizontalExpand = true });
        iconCenterRow.AddChild(iconContainer);
        iconCenterRow.AddChild(new Control { HorizontalExpand = true });
        iconCenterRow.MouseFilter = MouseFilterMode.Stop;
        iconCenterRow.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick || _selectedKnowledgeId == null) return;
            args.Handle();
            OnPathSelected?.Invoke(_selectedKnowledgeId);
        };

        _complexityLabel = new Label
        {
            FontColorOverride = Color.FromHex("#999999"),
            HorizontalExpand = true,
            Align = Label.AlignMode.Center,
        };

        var selDescHeader = new Label
        {
            Text = Loc.GetString("heretic-info-desc-header"),
            FontColorOverride = Color.FromHex("#aaaaaa"),
            HorizontalExpand = true,
            Align = Label.AlignMode.Center,
        };

        _descLabel = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(0, 4, 0, 0) };

        _passiveHeaderLabel = new Label
        {
            Text = Loc.GetString("heretic-path-select-passive-header"),
            FontColorOverride = Color.FromHex("#aaaaaa"),
        };

        _passiveNameLabel = new Label { FontColorOverride = Color.FromHex("#cccccc") };
        _passiveDescLabel = new RichTextLabel { HorizontalExpand = true };
        var passiveBox = new PanelContainer { HorizontalExpand = true };
        var passiveBoxStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#152a1a"),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };
        passiveBox.PanelOverride = passiveBoxStyle;
        passiveBox.AddChild(_passiveDescLabel);

        var prosHeader = new Label
        {
            Text = Loc.GetString("heretic-path-select-pros-header"),
            FontColorOverride = HeaderGold,
        };

        _prosLabel = new RichTextLabel { HorizontalExpand = true };

        var consHeader = new Label
        {
            Text = Loc.GetString("heretic-path-select-cons-header"),
            FontColorOverride = HeaderGold,
        };

        _consLabel = new RichTextLabel { HorizontalExpand = true };

        _guaranteedHeader = new Label
        {
            Text = Loc.GetString("heretic-info-path-guaranteed"),
            FontColorOverride = HeaderGold,
            Visible = false,
        };

        _guaranteedRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };

        _guaranteedBg = new PanelContainer
        {
            HorizontalExpand = true,
            Visible = false,
            Margin = new Thickness(0, 2),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#1a1a2e"),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
        };
        _guaranteedBg.AddChild(_guaranteedRow);

        selectionPanel.AddChild(_pathNameLabel);
        selectionPanel.AddChild(choosePathLabel);
        selectionPanel.AddChild(iconCenterRow);
        selectionPanel.AddChild(_complexityLabel);
        selectionPanel.AddChild(selDescHeader);
        selectionPanel.AddChild(_descLabel);
        selectionPanel.AddChild(MakeSeparator());
        selectionPanel.AddChild(_passiveHeaderLabel);
        selectionPanel.AddChild(passiveBox);
        selectionPanel.AddChild(MakeSeparator());
        selectionPanel.AddChild(_guaranteedHeader);
        selectionPanel.AddChild(_guaranteedBg);
        selectionPanel.AddChild(MakeSeparator());
        selectionPanel.AddChild(prosHeader);
        selectionPanel.AddChild(_prosLabel);
        selectionPanel.AddChild(MakeSeparator());
        selectionPanel.AddChild(consHeader);
        selectionPanel.AddChild(_consLabel);

        _selectionScroll.AddChild(selectionPanel);

        // ─── Selected panel (CurrentPath != General) ─────────────────────────
        _selectedScroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };

        var selectedPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            HorizontalExpand = true,
        };

        _selectedPathNameLabel = new Label
        {
            FontColorOverride = HeaderGold,
            Align = Label.AlignMode.Center,
            HorizontalExpand = true,
        };

        _selectedDescLabel = new RichTextLabel { HorizontalExpand = true };

        _passiveAbilityLabel = new Label { FontColorOverride = Color.FromHex("#cccccc") };

        _passiveProgressBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };

        (_level1Panel, _level1TitleLabel, _level1DescLabel) = MakeLevelBox();
        (_level2Panel, _level2TitleLabel, _level2DescLabel) = MakeLevelBox();
        (_level3Panel, _level3TitleLabel, _level3DescLabel) = MakeLevelBox();

        _passiveProgressBox.AddChild(_level1Panel);
        _passiveProgressBox.AddChild(_level2Panel);
        _passiveProgressBox.AddChild(_level3Panel);

        _tipsHeader = new Label
        {
            Text = Loc.GetString("heretic-info-path-tips"),
            FontColorOverride = HeaderGold,
        };

        _tipsLabel = new RichTextLabel { HorizontalExpand = true };

        selectedPanel.AddChild(_selectedPathNameLabel);
        selectedPanel.AddChild(MakeSeparator());
        selectedPanel.AddChild(new Label
        {
            Text = Loc.GetString("heretic-info-desc-header"),
            FontColorOverride = Color.FromHex("#aaaaaa"),
            Align = Label.AlignMode.Center,
            HorizontalExpand = true,
        });
        selectedPanel.AddChild(_selectedDescLabel);
        selectedPanel.AddChild(MakeSeparator());
        selectedPanel.AddChild(_passiveAbilityLabel);
        selectedPanel.AddChild(_passiveProgressBox);
        selectedPanel.AddChild(MakeSeparator());
        selectedPanel.AddChild(_tipsHeader);
        selectedPanel.AddChild(_tipsLabel);

        _selectedScroll.AddChild(selectedPanel);

        tab2Root.AddChild(_leftScroll);
        tab2Root.AddChild(_selectionScroll);
        tab2Root.AddChild(_selectedScroll);
        var tab2Panel = new GradientPanel(4, 4);
        tab2Panel.AddChild(tab2Root);
        tabs.AddChild(tab2Panel);

        // ─── Tab 3: research graph ───────────────────────────────────────────
        var tab3Root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(8),
            SeparationOverride = 8,
        };

        // left column: labels + graph + gift row + ascension (fixed width, horizontal scroll)
        var leftCol = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 380,
            VerticalExpand = true,
        };

        _pointsLabel = new RichTextLabel { HorizontalExpand = true };

        leftCol.AddChild(_pointsLabel);
        leftCol.AddChild(new PanelContainer { MinSize = new Vector2(0, 1), Margin = new Thickness(0, 2) });
        leftCol.AddChild(new Label
        {
            Text = Loc.GetString("heretic-info-knowledge-tree"),
            FontColorOverride = HeaderGold,
        });
        leftCol.AddChild(new PanelContainer { MinSize = new Vector2(0, 1) });

        var graphScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            VScrollEnabled = true,
        };
        _treeBox = new KnowledgeGraphControl();
        graphScroll.AddChild(_treeBox);
        leftCol.AddChild(graphScroll);

        _ascensionPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(0, 4, 0, 0),
        };
        leftCol.AddChild(_ascensionPanel);

        tab3Root.AddChild(leftCol);

        // right column: knowledge shop
        _shopScroll = new ScrollContainer
        {
            MinWidth = 260,
            HorizontalExpand = true,
            VerticalExpand = true,
            Visible = false,
        };

        _shopPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        _shopScroll.AddChild(_shopPanel);
        tab3Root.AddChild(_shopScroll);

        var tab3Panel = new GradientPanel(4, 4);
        tab3Panel.AddChild(tab3Root);
        tabs.AddChild(tab3Panel);

        tabs.SetTabTitle(0, Loc.GetString("heretic-info-tab-info"));
        tabs.SetTabTitle(1, Loc.GetString("heretic-info-tab-path"));
        tabs.SetTabTitle(2, Loc.GetString("heretic-info-tab-research"));

        // Wrap tabs in dark panel so the tab-button strip isn't grey
        var tabsWrapper = new PanelContainer { HorizontalExpand = true, VerticalExpand = true };
        tabsWrapper.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#0a0118") };
        tabsWrapper.AddChild(tabs);
        Contents.AddChild(tabsWrapper);
    }

    private static (PanelContainer panel, Label title, RichTextLabel desc) MakeLevelBox()
    {
        var title = new Label { FontColorOverride = LockGray };
        var desc = new RichTextLabel { HorizontalExpand = true };
        desc.Modulate = LockGray;

        var inner = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Margin = new Thickness(6, 4),
        };
        inner.AddChild(title);
        inner.AddChild(desc);

        var style = new StyleBoxFlat
        {
            BackgroundColor = LockGray.WithAlpha(0.10f),
            BorderColor = LockGray,
            BorderThickness = new Thickness(1),
        };

        var panel = new PanelContainer { HorizontalExpand = true };
        panel.PanelOverride = style;
        panel.AddChild(inner);

        return (panel, title, desc);
    }

    private void SetLevelBoxColor(PanelContainer panel, Label title, RichTextLabel desc, Color color)
    {
        var style = new StyleBoxFlat
        {
            BackgroundColor = color.WithAlpha(0.12f),
            BorderColor = color,
            BorderThickness = new Thickness(1),
        };
        panel.PanelOverride = style;
        title.FontColorOverride = color;
        desc.Modulate = color;
    }

    public void Populate(HereticInfoBuiState state)
    {
        _state = state;

        PopulateInfoTab(state);
        PopulatePathTab(state);
        PopulateResearchTab(state);
    }

    private void PopulateInfoTab(HereticInfoBuiState state)
    {
        _infoKpLabel.Text = Loc.GetString("heretic-info-kp-label", ("count", state.KnowledgePoints));
        _infoSacrificeLabel.Text = Loc.GetString("heretic-info-sacrifice-count", ("count", state.SacrificeCount));

        _infoTasksBox.RemoveAllChildren();
        var pathDone = state.CurrentPath != HereticPath.General;
        var sacrificeDone = state.RequiredSacrifices > 0 && state.SacrificeCount >= state.RequiredSacrifices;
        var researchedCount = state.Nodes.Count(n => n.IsResearched);
        var knowledgeDone = state.RequiredKnowledge > 0 && researchedCount >= state.RequiredKnowledge;
        var ascensionResearched = state.Nodes.Any(n =>
            n.Id.StartsWith("KnowledgeAscension", StringComparison.OrdinalIgnoreCase) && n.IsResearched);

        if (pathDone && sacrificeDone && knowledgeDone && ascensionResearched)
        {
            _infoTasksBox.AddChild(new Label
            {
                Text = Loc.GetString("heretic-info-tasks-none"),
                FontColorOverride = BrightGreen,
            });
        }
        else
        {
            _infoTasksBox.AddChild(MakeTaskLabel(1, Loc.GetString("heretic-info-task-path-chosen"), pathDone));
            _infoTasksBox.AddChild(MakeTaskLabel(2, Loc.GetString("heretic-info-task-sacrifices", ("current", state.SacrificeCount), ("required", state.RequiredSacrifices)), sacrificeDone));
            _infoTasksBox.AddChild(MakeTaskLabel(3, Loc.GetString("heretic-info-task-knowledge", ("current", researchedCount), ("required", state.RequiredKnowledge)), knowledgeDone));
            _infoTasksBox.AddChild(MakeTaskLabel(4, Loc.GetString("heretic-info-task-ascension-researched"), ascensionResearched));
        }
    }

    private static Label MakeTaskLabel(int num, string text, bool done) => new()
    {
        Text = $"#{num}: {text}",
        FontColorOverride = done ? BrightGreen : Color.White,
    };

    private static PanelContainer MakeSeparator() => new()
    {
        MinSize = new Vector2(0, 1),
        Margin = new Thickness(0, 2),
        PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#ffffff33") },
    };

    private static SpriteSpecifier.Rsi? BladeSpecForPath(HereticPath path) => path switch
    {
        HereticPath.Ash    => new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/blade_ash.rsi"),     "ash_blade"),
        HereticPath.Moon   => new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/blade_moon.rsi"),    "moon_blade"),
        HereticPath.Lock   => new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/blade_lock.rsi"),    "key_blade"),
        HereticPath.Flesh  => new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/blade_flesh.rsi"),   "flesh_blade"),
        HereticPath.Void   => new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/blade_void.rsi"),    "void_blade"),
        HereticPath.Blade  => new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/blade_sundered.rsi"),"dark_blade"),
        HereticPath.Rust   => new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/blade_rust.rsi"),    "rust_blade"),
        HereticPath.Cosmos => new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/blade_cosmos.rsi"),  "cosmic_blade"),
        _                  => null,
    };

    private static string? BackgroundStateForPath(HereticPath path) => path switch
    {
        HereticPath.Ash    => "node_ash",
        HereticPath.Moon   => "node_moon",
        HereticPath.Lock   => "node_lock",
        HereticPath.Void   => "node_void",
        HereticPath.Blade  => "node_blade",
        HereticPath.Rust   => "node_rust",
        HereticPath.Cosmos => "node_cosmos",
        _                  => null,
    };

    private readonly record struct GuaranteedEntry(string NodeId, SpriteSpecifier? Override = null);

    private static readonly Dictionary<HereticPath, GuaranteedEntry[]> GuaranteedAbilitiesPerPath = new()
    {
        [HereticPath.Ash] = new GuaranteedEntry[]
        {
            new("KnowledgeAshenPassage"),
            new("KnowledgeVolcanoBlast"),
            new("KnowledgeMaskOfMadnessAsh"),
            new("KnowledgeAshlordsRebirth"),
        },
        [HereticPath.Moon] = new GuaranteedEntry[]
        {
            new("KnowledgeMoonGate"),
            new("KnowledgeMoonAmulet",            new SpriteSpecifier.EntityPrototype("HereticMoonAmulet")),
            new("KnowledgeMoonParade"),
            new("KnowledgeMoonRingleader"),
        },
        [HereticPath.Lock] = new GuaranteedEntry[]
        {
            new("KnowledgeKeyRing",               new SpriteSpecifier.EntityPrototype("CaptainIDCard")),
            new("KnowledgeLockLabyrinthHandbook",  new SpriteSpecifier.EntityPrototype("HereticLabyrinthHandbook")),
            new("KnowledgeBurglarFinesse"),
            new("KnowledgeCaretakerRefuge"),
        },
        [HereticPath.Flesh] = new GuaranteedEntry[]
        {
            new("KnowledgeImperfectRitual",  new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/knowledge.rsi"),     "ghoul_voiceless")),
            new("KnowledgeFleshWeave",       new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "mad_touch")),
            new("KnowledgeRawProphet",       new SpriteSpecifier.EntityPrototype("MobHereticRawProphet")),
            new("KnowledgeRitualOfSolitude", new SpriteSpecifier.EntityPrototype("HereticFamiliarStalker")),
        },
        [HereticPath.Void] = new GuaranteedEntry[]
        {
            new("KnowledgeVoidPhase",   new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "voidblink")),
            new("KnowledgeVoidPrison"),
            new("KnowledgeVoidPull",    new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "voidpull")),
            new("KnowledgeVoidWeave",   new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "void_rift")),
        },
        [HereticPath.Blade] = new GuaranteedEntry[]
        {
            new("KnowledgeRealignment"),
            new("KnowledgeBladeStanceOfTornChampion"),
            new("KnowledgeFuriousSteel"),
            new("KnowledgeWolvesAmongSheep"),
        },
        [HereticPath.Rust] = new GuaranteedEntry[]
        {
            new("KnowledgeMarkOfRust", new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "corrode")),
            new("KnowledgeRustConstruction", new SpriteSpecifier.Rsi(new ResPath("/Textures/Structures/Walls/solid_rust.rsi"), "full")),
            new("KnowledgeRustEntropicPlume"),
            new("KnowledgeRustDash"),
        },
        [HereticPath.Cosmos] = new GuaranteedEntry[]
        {
            new("KnowledgeCosmicRune", new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "cosmic_rune")),
            new("KnowledgeStarBlast", new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "star_blast")),
            new("KnowledgeStarTouch", new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "star_touch")),
            new("KnowledgeCosmicExpansion", new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/heretic/actions_ecult.rsi"), "cosmic_domain")),
        },
    };

    private void PopulatePathTab(HereticInfoBuiState state)
    {
        _pathList.RemoveAllChildren();
        _activeButton = null;
        _selectedKnowledgeId = null;

        var isGeneral = state.CurrentPath == HereticPath.General;
        _leftScroll.Visible = true;
        _selectionScroll.Visible = isGeneral;
        _selectedScroll.Visible = !isGeneral;

        if (isGeneral)
        {
            _pathIconRect.Texture = null;
            _bladeIconRect.Texture = null;
            _bladeIconRect.Visible = false;
            _pathNameLabel.Text = Loc.GetString("heretic-path-select-placeholder");
            _complexityLabel.Text = string.Empty;
            _descLabel.Text = string.Empty;
            _passiveHeaderLabel.Text = Loc.GetString("heretic-path-select-passive-header");
            _passiveDescLabel.Text = string.Empty;
            _prosLabel.Text = string.Empty;
            _consLabel.Text = string.Empty;
            _guaranteedRow.RemoveAllChildren();
            _guaranteedHeader.Visible = false;
            _guaranteedBg.Visible = false;

            foreach (var path in state.Paths)
            {
                var captured = path;
                var btn = new Button
                {
                    Text = path.Name,
                    HorizontalExpand = true,
                    MinSize = new Vector2(0, 36),
                };
                btn.OnPressed += _ => SelectPath(captured, btn);
                _pathList.AddChild(btn);
            }
        }
        else
        {
            foreach (var path in state.Paths)
            {
                var isSelected = path.Path == state.CurrentPath;
                _pathList.AddChild(new Label
                {
                    Text = path.Name,
                    HorizontalExpand = true,
                    FontColorOverride = isSelected ? BrightGreen : Color.FromHex("#cccccc"),
                });
            }
            var currentPath = state.Paths.FirstOrDefault(p => p.Path == state.CurrentPath);
            if (currentPath != null)
                ShowSelectedPath(currentPath, state);
        }
    }

    private void SelectPath(HereticPathData path, Button btn)
    {
        if (_activeButton != null)
            _activeButton.Disabled = false;

        _activeButton = btn;
        btn.Disabled = true;
        _selectedKnowledgeId = path.PathKnowledgeId;

        if (path.Icon != null)
            _pathIconRect.Texture = _spriteSystem.Frame0(path.Icon);
        else
            _pathIconRect.Texture = null;

        var bladeSpec = BladeSpecForPath(path.Path);
        if (bladeSpec != null)
        {
            _bladeIconRect.Texture = _spriteSystem.Frame0(bladeSpec);
            _bladeIconRect.Visible = true;
        }
        else
        {
            _bladeIconRect.Texture = null;
            _bladeIconRect.Visible = false;
        }

        _pathNameLabel.Text = path.Name;
        var low = Loc.GetString("heretic-path-complexity-low");
        var medium = Loc.GetString("heretic-path-complexity-medium");
        _complexityLabel.FontColorOverride = path.Complexity == low ? BrightGreen
            : path.Complexity == medium ? Color.FromHex("#cccc55")
            : Color.FromHex("#cc5555");
        _complexityLabel.Text = Loc.GetString("heretic-path-select-complexity", ("value", path.Complexity));
        _descLabel.Text = path.Description;
        _passiveHeaderLabel.Text = $"{Loc.GetString("heretic-path-select-passive-header")} {path.PassiveName}";
        _passiveDescLabel.Text = path.PassiveDescription;
        _prosLabel.Text = path.Pros;
        _consLabel.Text = path.Cons;

        GuaranteedAbilitiesPerPath.TryGetValue(path.Path, out var entries);
        entries ??= Array.Empty<GuaranteedEntry>();

        _guaranteedRow.RemoveAllChildren();
        _guaranteedHeader.Visible = entries.Length > 0;
        _guaranteedBg.Visible = entries.Length > 0;

        foreach (var entry in entries)
        {
            var node = _state?.Nodes.FirstOrDefault(n => n.Id == entry.NodeId);
            var iconSpec = entry.Override
                ?? node?.Icon
                ?? (SpriteSpecifier)new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/heretic/knowledge.rsi"),
                    BackgroundStateForPath(path.Path) ?? "node_void");

            var tooltip = node != null
                ? (string.IsNullOrEmpty(node.Description) ? node.Name : $"{node.Name}\n{node.Description}")
                : entry.NodeId;

            var iconRect = MakeIcon(iconSpec, 48, 48);

            var slot = new PanelContainer
            {
                ToolTip = tooltip,
                MouseFilter = MouseFilterMode.Stop,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#252545"),
                    BorderColor = Color.FromHex("#44446a"),
                    BorderThickness = new Thickness(1),
                    ContentMarginLeftOverride = 4,
                    ContentMarginRightOverride = 4,
                    ContentMarginTopOverride = 4,
                    ContentMarginBottomOverride = 4,
                },
            };
            slot.AddChild(iconRect);
            _guaranteedRow.AddChild(slot);
        }
    }

    private void ShowSelectedPath(HereticPathData path, HereticInfoBuiState state)
    {
        _selectedPathNameLabel.Text = path.Name;
        _selectedDescLabel.Text = path.Description;
        _passiveAbilityLabel.Text = Loc.GetString("heretic-info-passive-ability",
            ("name", path.PassiveName), ("level", state.PassiveLevel));

        var level = state.PassiveLevel;

        _level1TitleLabel.Text = Loc.GetString("heretic-info-passive-level-1");
        _level1DescLabel.Text = path.Level1Description;
        SetLevelBoxColor(_level1Panel, _level1TitleLabel, _level1DescLabel,
            level >= 1 ? BrightGreen : LockGray);

        _level2TitleLabel.Text = Loc.GetString("heretic-info-passive-level-2");
        _level2DescLabel.Text = path.Level2Description;
        SetLevelBoxColor(_level2Panel, _level2TitleLabel, _level2DescLabel,
            level >= 2 ? BrightGreen : LockGray);

        _level3TitleLabel.Text = Loc.GetString("heretic-info-passive-level-3");
        _level3DescLabel.Text = path.Level3Description;
        SetLevelBoxColor(_level3Panel, _level3TitleLabel, _level3DescLabel,
            level >= 3 ? BrightGreen : LockGray);

        _tipsLabel.Text = path.Tips;
        _tipsHeader.Visible = !string.IsNullOrEmpty(path.Tips);
    }

    private static readonly string[] InitialNodeOrder =
    {
        "KnowledgeMansusGrasp",
        "KnowledgeCloakOfShadow",
        "KnowledgeAmberFocus",
        "KnowledgeLivingHeart",
        "KnowledgeFeastOfOwls",
        "KnowledgeHeartbeatMansus",
    };

    private void PopulateResearchTab(HereticInfoBuiState state)
    {
        _pointsLabel.Text = Loc.GetString("heretic-knowledge-points", ("count", state.KnowledgePoints));
        var nodeById = state.Nodes.ToDictionary(n => n.Id);
        _treeBox.Reset();

        // РАССВЕТ header
        var dawnLabel = new Label
        {
            Text = "РАССВЕТ",
            Align = Label.AlignMode.Center,
            HorizontalExpand = true,
        };
        dawnLabel.AddStyleClass(StyleClass.LabelHeadingBigger);
        _treeBox.AddChild(dawnLabel);

        // General nodes in fixed order, max 4 per row
        var generalWrappers = new List<Control>();
        foreach (var id in InitialNodeOrder)
        {
            if (!nodeById.TryGetValue(id, out var node))
                continue;
            var captured = node;
            var isBuyable = !node.IsResearched && node.PrerequisitesMet;
            var wrapper = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 2,
                ToolTip = $"{node.Name}\n{node.Description}",
            };
            var badgeText = isBuyable
                ? ((node.IsGift || node.Cost == 0) ? "ДАР" : node.Cost.ToString())
                : " ";
            wrapper.AddChild(new Label
            {
                Text = badgeText,
                FontColorOverride = (node.IsGift || node.Cost == 0) ? HeaderGold : Color.White,
                Align = Label.AlignMode.Center,
            });
            var nodeCtrl = new KnowledgeNodeControl(_spriteSystem, node, state.CurrentPath);
            if (isBuyable)
                nodeCtrl.OnActivated += () => OnKnowledgeSelected?.Invoke(captured.Id);
            wrapper.AddChild(nodeCtrl);
            _treeBox.RegisterNode(node.Id, nodeCtrl, node.IsResearched);
            generalWrappers.Add(wrapper);
        }
        for (var gi = 0; gi < generalWrappers.Count; gi += 4)
        {
            var generalRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                HorizontalAlignment = HAlignment.Center,
            };
            for (var gj = gi; gj < Math.Min(gi + 4, generalWrappers.Count); gj++)
                generalRow.AddChild(generalWrappers[gj]);
            _treeBox.AddChild(generalRow);
        }

        if (state.CurrentPath != HereticPath.General)
        {
            var pathNodes = state.Nodes.Where(n => n.Path == state.CurrentPath).ToList();
            var pathById = pathNodes.ToDictionary(n => n.Id);
            var tierOf = new Dictionary<string, int>();

            int GetTier(HereticKnowledgeNodeData node, HashSet<string> visiting)
            {
                if (tierOf.TryGetValue(node.Id, out var cached))
                    return cached;
                if (!visiting.Add(node.Id))
                    return 0;
                var prereqIds = node.Prerequisites
                    .Concat(node.PrerequisitesAny.SelectMany(g => g))
                    .Distinct();
                var maxPre = -1;
                foreach (var pid in prereqIds)
                {
                    if (!pathById.TryGetValue(pid, out var pn))
                        continue;
                    var t = GetTier(pn, visiting);
                    if (t > maxPre)
                        maxPre = t;
                }
                visiting.Remove(node.Id);
                var result = maxPre < 0 ? 0 : maxPre + 1;
                tierOf[node.Id] = result;
                return result;
            }

            foreach (var node in pathNodes)
                GetTier(node, new HashSet<string>());

            var maxTier = tierOf.Values.DefaultIfEmpty(0).Max();
            var tiers = Enumerable.Range(0, maxTier + 1)
                .Select(_ => new List<HereticKnowledgeNodeData>())
                .ToList();
            foreach (var node in pathNodes)
                if (tierOf.TryGetValue(node.Id, out var ti))
                    tiers[ti].Add(node);

            // Show tiers up to "highest accessible tier + 1" (one look-ahead)
            var currentActiveTier = 0;
            for (var i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].Any(n => n.PrerequisitesMet || n.IsResearched))
                    currentActiveTier = i;
            }
            var showUpToTier = Math.Min(currentActiveTier + 1, tiers.Count - 1);

            for (var i = 0; i <= showUpToTier; i++)
            {
                var sep = new PanelContainer { MinSize = new Vector2(0, 2) };
                sep.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#ffffff40") };
                _treeBox.AddChild(sep);
                var tierWrappers = new List<Control>();
                foreach (var node in tiers[i])
                {
                    var captured = node;
                    var isAscension = node.Id.StartsWith("KnowledgeAscension", StringComparison.OrdinalIgnoreCase);
                    var isBuyable = !node.IsResearched && node.PrerequisitesMet;
                    var wrapper = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        SeparationOverride = 2,
                        ToolTip = $"{node.Name}\n{node.Description}",
                    };
                    if (!isAscension)
                    {
                        var badgeText = isBuyable
                            ? ((node.IsGift || node.Cost == 0) ? "ДАР" : node.Cost.ToString())
                            : " ";
                        wrapper.AddChild(new Label
                        {
                            Text = badgeText,
                            FontColorOverride = (node.IsGift || node.Cost == 0) ? HeaderGold : Color.White,
                            Align = Label.AlignMode.Center,
                        });
                    }
                    var nodeCtrl = new KnowledgeNodeControl(_spriteSystem, node, state.CurrentPath);
                    if (isBuyable)
                        nodeCtrl.OnActivated += () => OnKnowledgeSelected?.Invoke(captured.Id);
                    wrapper.AddChild(nodeCtrl);
                    _treeBox.RegisterNode(node.Id, nodeCtrl, node.IsResearched);
                    if (isAscension)
                    {
                        wrapper.AddChild(new Label
                        {
                            Text = "ЗАКАТ",
                            FontColorOverride = Color.FromHex("#aa55cc"),
                            Align = Label.AlignMode.Center,
                        });
                    }
                    tierWrappers.Add(wrapper);
                }
                for (var ti = 0; ti < tierWrappers.Count; ti += 4)
                {
                    var row = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        SeparationOverride = 8,
                        HorizontalAlignment = HAlignment.Center,
                    };
                    for (var tj = ti; tj < Math.Min(ti + 4, tierWrappers.Count); tj++)
                        row.AddChild(tierWrappers[tj]);
                    _treeBox.AddChild(row);
                }

                // Gift groups whose source node is in this tier appear immediately after it
                var tierNodeIds = new HashSet<string>(tiers[i].Select(n => n.Id));
                foreach (var giftGroup in state.PendingGiftGroups)
                {
                    if (!tierNodeIds.Contains(giftGroup.SourceNodeId))
                        continue;

                    var groupNodes = giftGroup.Candidates
                        .Select(id => state.Nodes.FirstOrDefault(n => n.Id == id))
                        .Where(n => n != null)
                        .ToList();
                    if (groupNodes.Count == 0)
                        continue;

                    var giftSep = new PanelContainer { MinSize = new Vector2(0, 2) };
                    giftSep.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#ffffff40") };
                    _treeBox.AddChild(giftSep);

                    var giftRow = new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        SeparationOverride = 8,
                        HorizontalAlignment = HAlignment.Center,
                    };
                    foreach (var giftNode in groupNodes)
                    {
                        var captured = giftNode!;
                        var giftWrapper = new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Vertical,
                            SeparationOverride = 2,
                            ToolTip = $"{captured.Name}\n{captured.Description}",
                        };
                        giftWrapper.AddChild(new Label
                        {
                            Text = "ДАР",
                            FontColorOverride = HeaderGold,
                            Align = Label.AlignMode.Center,
                        });
                        var giftCtrl = new KnowledgeNodeControl(_spriteSystem, captured, state.CurrentPath, canAfford: true);
                        giftCtrl.OnActivated += () => OnKnowledgeSelected?.Invoke(captured.Id);
                        giftWrapper.AddChild(giftCtrl);
                        _treeBox.RegisterNode(captured.Id, giftCtrl, false);
                        giftRow.AddChild(giftWrapper);
                    }
                    _treeBox.AddChild(giftRow);
                }
            }

        }

        // Ascension status panel
        _ascensionPanel.RemoveAllChildren();
        _ascensionPanel.Visible = state.CurrentPath != HereticPath.General;

        if (state.CurrentPath != HereticPath.General)
        {
            var ascSpec = new SpriteSpecifier.Rsi(
                new ResPath("/Textures/Imperial/heretic/ascension.rsi"),
                AscensionState(state.CurrentPath));
            _ascensionPanel.AddChild(MakeIcon(ascSpec, 64, 64));

            var textCol = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                VerticalExpand = true,
                HorizontalExpand = true,
                SeparationOverride = 4,
            };
            textCol.AddChild(new Label
            {
                Text = Loc.GetString("heretic-ascension-header"),
                FontColorOverride = HeaderGold,
            });

            var ascAchieved = state.Nodes.Any(n =>
                n.Id.StartsWith("KnowledgeAscension", StringComparison.OrdinalIgnoreCase) && n.IsResearched);

            textCol.AddChild(new Label
            {
                Text = ascAchieved
                    ? Loc.GetString("heretic-ascension-achieved")
                    : Loc.GetString("heretic-ascension-pending"),
                FontColorOverride = ascAchieved ? BrightGreen : LockGray,
            });

            _ascensionPanel.AddChild(textCol);
        }

        // Knowledge shop
        _shopPanel.RemoveAllChildren();

        if (state.CurrentShopLevel <= 0)
        {
            _shopScroll.Visible = false;
        }
        else
        {
            _shopScroll.Visible = true;

            _shopPanel.AddChild(new Label
            {
                Text = Loc.GetString("heretic-info-knowledge-shop"),
                FontColorOverride = HeaderGold,
                Align = Label.AlignMode.Center,
            });
            _shopPanel.AddChild(new PanelContainer
            {
                MinSize = new Vector2(0, 1),
                Margin = new Thickness(0, 2),
            });

            // Level sections — shown as icon grids (gifts are in the tree, not here)
            var researchedIds = state.Nodes
                .Where(n => n.IsResearched)
                .Select(n => n.Id)
                .ToHashSet();
            for (var lvl = 1; lvl <= state.CurrentShopLevel; lvl++)
            {
                var levelNodes = state.Nodes
                    .Where(n => n.ShopLevel == lvl
                        && !n.ConflictsWith.Any(c => researchedIds.Contains(c)))
                    .OrderBy(n => n.Name)
                    .ToList();
                if (levelNodes.Count == 0) continue;

                _shopPanel.AddChild(new PanelContainer
                {
                    HorizontalExpand = true,
                    MinSize = new Vector2(0, 1),
                    Margin = new Thickness(0, 4, 0, 0),
                    PanelOverride = new StyleBoxFlat { BackgroundColor = HeaderGold },
                });
                _shopPanel.AddChild(new Label
                {
                    Text = Loc.GetString("heretic-info-shop-level", ("level", lvl)),
                    FontColorOverride = HeaderGold,
                    Margin = new Thickness(0, 2, 0, 2),
                });
                _shopPanel.AddChild(new PanelContainer
                {
                    HorizontalExpand = true,
                    MinSize = new Vector2(0, 1),
                    Margin = new Thickness(0, 0, 0, 6),
                    PanelOverride = new StyleBoxFlat { BackgroundColor = HeaderGold },
                });
                AddShopIconGrid(levelNodes, state);
            }
            _shopPanel.AddChild(new PanelContainer
            {
                HorizontalExpand = true,
                MinSize = new Vector2(0, 1),
                Margin = new Thickness(0, 4, 0, 0),
                PanelOverride = new StyleBoxFlat { BackgroundColor = HeaderGold },
            });
        }
    }

    private void AddShopIconGrid(List<HereticKnowledgeNodeData> nodes, HereticInfoBuiState state)
    {
        const int perRow = 4;
        const int nodeSize = 72;

        for (var i = 0; i < nodes.Count; i += perRow)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                Margin = new Thickness(0, 0, 0, 4),
            };

            for (var j = i; j < Math.Min(i + perRow, nodes.Count); j++)
            {
                var node = nodes[j];
                var captured = node;
                var canAfford = state.KnowledgePoints >= node.Cost;

                var wrapper = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 2,
                    ToolTip = $"{node.Name}\n{node.Description}",
                };

                // cost badge above the icon
                var badgeText = node.IsResearched ? " " : node.Cost.ToString();
                wrapper.AddChild(new Label
                {
                    Text = badgeText,
                    FontColorOverride = canAfford ? Color.White : LockGray,
                    Align = Label.AlignMode.Center,
                });

                var nodeCtrl = new KnowledgeNodeControl(_spriteSystem, node, state.CurrentPath, canAfford, nodeSize, forceGreen: true);
                if (!node.IsResearched && canAfford)
                    nodeCtrl.OnActivated += () => OnKnowledgeSelected?.Invoke(captured.Id);

                wrapper.AddChild(nodeCtrl);
                row.AddChild(wrapper);
            }

            _shopPanel.AddChild(row);
        }
    }

    private Control MakeShopButton(HereticKnowledgeNodeData node, HereticInfoBuiState state, bool isGift)
    {
        var isResearched = node.IsResearched;
        var canAfford = isGift || state.KnowledgePoints >= node.Cost;

        var btn = new Button
        {
            HorizontalExpand = true,
            Disabled = isResearched || (!isGift && !canAfford),
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };
        btn.AddChild(row);

        if (node.Icon != null)
            row.AddChild(MakeIcon(node.Icon, 24, 24));

        row.AddChild(new Label
        {
            Text = node.Name,
            HorizontalExpand = true,
            ClipText = true,
            FontColorOverride = isResearched
                ? DimGreen
                : isGift
                    ? Color.FromHex("#cc88ff")
                    : Color.White,
        });

        row.AddChild(new Label
        {
            Text = isResearched
                ? Loc.GetString("heretic-info-shop-learned")
                : isGift
                    ? Loc.GetString("heretic-info-shop-learn")
                    : $"{node.Cost} оч.",
            FontColorOverride = isResearched ? DimGreen : canAfford ? BrightGreen : LockGray,
        });

        if (!isResearched)
        {
            var capturedId = node.Id;
            btn.OnPressed += _ => OnKnowledgeSelected?.Invoke(capturedId);
        }

        return btn;
    }

    private TextureRect MakeIcon(SpriteSpecifier spec, int w, int h) => new()
    {
        Texture = _spriteSystem.Frame0(spec),
        MinSize = new Vector2(w, h),
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
    };

    private static string AscensionState(HereticPath path) => path switch
    {
        HereticPath.Ash => "ashascend",
        HereticPath.Moon => "moonascend",
        HereticPath.Lock => "lockascend",
        HereticPath.Flesh => "fleshascend",
        HereticPath.Void => "voidascend",
        HereticPath.Blade => "bladeascend",
        HereticPath.Rust => "rustascend",
        HereticPath.Cosmos => "cosmicascend",
        _ => "ashascend",
    };

    private static string PathNodeState(HereticPath path) => path switch
    {
        HereticPath.General => "node_side",
        HereticPath.Ash => "node_ash",
        HereticPath.Moon => "node_moon",
        HereticPath.Lock => "node_lock",
        HereticPath.Flesh => "node_flesh",
        HereticPath.Void => "node_void",
        HereticPath.Blade => "node_blade",
        HereticPath.Rust => "node_rust",
        HereticPath.Cosmos => "node_cosmos",
        _ => "node_side",
    };

    private sealed class KnowledgeGraphControl : BoxContainer
    {
        private readonly Dictionary<string, (Control Ctrl, bool IsResearched)> _nodeMap = new();

        public KnowledgeGraphControl()
        {
            Orientation = LayoutOrientation.Vertical;
            SeparationOverride = 8;
            HorizontalExpand = true;
        }

        public void Reset()
        {
            RemoveAllChildren();
            _nodeMap.Clear();
        }

        public void RegisterNode(string id, Control ctrl, bool isResearched)
        {
            _nodeMap[id] = (ctrl, isResearched);
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
            VerticalExpand = true;
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                ContentMarginLeftOverride = marginH,
                ContentMarginRightOverride = marginH,
                ContentMarginTopOverride = marginV,
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

    private sealed class KnowledgeNodeControl : Control
    {
        public event Action? OnActivated;

        private readonly bool _clickable;

        public KnowledgeNodeControl(SpriteSystem sprites, HereticKnowledgeNodeData node, HereticPath currentPath, bool canAfford = true, int size = 64, bool forceGreen = false)
        {
            var isAscension = node.Id.StartsWith("KnowledgeAscension", StringComparison.OrdinalIgnoreCase);
            var diameter = isAscension ? 192 : size;

            MinSize = new Vector2(diameter, diameter);
            MouseFilter = MouseFilterMode.Stop;

            _clickable = !node.IsResearched && node.PrerequisitesMet && canAfford;

            // Background circle sprite layer
            var bgState = forceGreen
                ? (!node.IsResearched && canAfford ? "node_finished" : "node_side")
                : node.IsResearched ? "node_finished"
                : !node.PrerequisitesMet ? "node_locked"
                : "node_side";

            var bgSpec = new SpriteSpecifier.Rsi(
                new ResPath("/Textures/Imperial/heretic/knowledge.rsi"),
                bgState);

            var layout = new LayoutContainer { MinSize = new Vector2(diameter, diameter) };
            AddChild(layout);

            var bgRect = new TextureRect
            {
                Texture = sprites.Frame0(bgSpec),
                Stretch = TextureRect.StretchMode.Scale,
            };
            layout.AddChild(bgRect);
            LayoutContainer.SetAnchorAndMarginPreset(bgRect, LayoutContainer.LayoutPreset.Wide);

            // Icon layer
            SpriteSpecifier iconSpec;
            if (isAscension)
                iconSpec = new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/heretic/ascension.rsi"),
                    AscensionState(currentPath));
            else if (node.Icon != null)
                iconSpec = node.Icon;
            else
                iconSpec = new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/heretic/knowledge.rsi"),
                    PathNodeState(node.Path));

            var pad = isAscension ? 24 : 8;
            var iconRect = new TextureRect
            {
                Texture = sprites.Frame0(iconSpec),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
            };
            layout.AddChild(iconRect);
            LayoutContainer.SetAnchorAndMarginPreset(iconRect, LayoutContainer.LayoutPreset.Wide);
            LayoutContainer.SetMarginLeft(iconRect, pad);
            LayoutContainer.SetMarginTop(iconRect, pad);
            LayoutContainer.SetMarginRight(iconRect, -pad);
            LayoutContainer.SetMarginBottom(iconRect, -pad);

            ToolTip = string.IsNullOrEmpty(node.Description)
                ? node.Name
                : $"{node.Name}\n{node.Description}";
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (_clickable && args.Function == EngineKeyFunctions.UIClick)
            {
                args.Handle();
                OnActivated?.Invoke();
            }
        }
    }
}
