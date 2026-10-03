using Robust.Shared.Utility;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Fission;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Fission;

[UsedImplicitly]
public sealed class FissionFabricatorBoundUserInterface : BoundUserInterface
{
    private FissionFabricatorWindow? _window;

    public FissionFabricatorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FissionFabricatorWindow>();
        _window.OnFabricate += id => SendMessage(new FissionFabricateMessage(id));
        _window.OnEject += (id, sheets) => SendMessage(new FissionEjectMaterialMessage(id, sheets));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FissionFabricatorUiState fabricator)
            _window?.UpdateState(fabricator);
    }
}

/// <summary>
/// Фабрикатор стержней 1 в 1 с tgui NuclearRodFabricator (SS220): окно 850×600, вкладки «Изготовление» и «Материалы»;
/// слева чертежи по трём вкладкам категорий, справа сведения о стержне, материалы и кнопка изготовления.
/// </summary>
public sealed class FissionFabricatorWindow : DefaultWindow
{
    public event Action<string>? OnFabricate;
    public event Action<string, int>? OnEject;

    private static readonly (FissionRodCategory Category, string Title, string Icon)[] Categories =
    {
        (FissionRodCategory.Fuel, "fission-fabricator-ui-fuel", "atom"),
        (FissionRodCategory.Moderator, "fission-fabricator-ui-moderator", "cubes"),
        (FissionRodCategory.Coolant, "fission-fabricator-ui-coolant", "snowflake"),
    };

    private readonly BoxContainer _fabricateTab;
    private readonly BoxContainer _materialsTab;
    private readonly BoxContainer _designList = Tgui.VBox(4);
    private readonly BoxContainer _info = Tgui.VBox(6);
    private readonly BoxContainer _storage = Tgui.VBox(4);

    private FissionFabricatorUiState? _state;
    private FissionRodCategory _category = FissionRodCategory.Fuel;
    private string? _selected;
    private string? _hovered;

    public FissionFabricatorWindow()
    {
        Title = Loc.GetString("fission-fabricator-ui-title");
        SetSize = new Vector2(850, 600);
        MinSize = new Vector2(700, 450);

        var root = Tgui.VBox(6);
        root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(root, scrollable: false));

        // Верхние вкладки.
        var tabs = new TguiTabs();
        root.AddChild(tabs);

        // ── Fabricate ──
        var categoryTabs = new TguiTabs();
        foreach (var (category, title, icon) in Categories)
        {
            var captured = category;
            categoryTabs.AddTab(Loc.GetString(title), icon, category == _category, () =>
            {
                _category = captured;
                RebuildDesigns();
            });
        }

        var designScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _designList } };
        var designBox = Tgui.VBox(6, categoryTabs, designScroll);
        designBox.VerticalExpand = true;
        var designs = Tgui.MakeSection(Loc.GetString("fission-fabricator-ui-designs"), null, designBox);
        designs.HorizontalExpand = true;
        designs.VerticalExpand = true;

        var infoScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _info } };
        var info = Tgui.MakeSection(Loc.GetString("fission-fabricator-ui-information"), null, infoScroll);
        info.HorizontalExpand = true;
        info.VerticalExpand = true;

        _fabricateTab = Tgui.HBox(8, designs, info);
        _fabricateTab.VerticalExpand = true;
        root.AddChild(_fabricateTab);

        // ── Materials ──
        var storageScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _storage } };
        var storageSection = Tgui.MakeSection(Loc.GetString("fission-fabricator-ui-storage"), null, storageScroll);
        storageSection.VerticalExpand = true;
        _materialsTab = Tgui.VBox(0, storageSection);
        _materialsTab.VerticalExpand = true;
        _materialsTab.Visible = false;
        root.AddChild(_materialsTab);

        tabs.AddTab(Loc.GetString("fission-fabricator-ui-tab-fabricate"), null, true, () =>
        {
            _fabricateTab.Visible = true;
            _materialsTab.Visible = false;
        });
        tabs.AddTab(Loc.GetString("fission-fabricator-ui-tab-materials"), null, false, () =>
        {
            _fabricateTab.Visible = false;
            _materialsTab.Visible = true;
        });
    }

    public void UpdateState(FissionFabricatorUiState state)
    {
        _state = state;
        RebuildDesigns();
        RebuildInfo();
        RebuildStorage();
    }

    #region Чертежи

    private void RebuildDesigns()
    {
        _designList.RemoveAllChildren();
        if (_state == null)
            return;

        var list = _state.Designs.Where(d => d.Category == _category).ToList();
        if (list.Count == 0)
        {
            var title = Loc.GetString(Categories.First(c => c.Category == _category).Title).ToLowerInvariant();
            _designList.AddChild(Tgui.Text(Loc.GetString("fission-fabricator-ui-none", ("category", title)), Tgui.Average));
            return;
        }

        foreach (var design in list)
        {
            _designList.AddChild(new DesignCard(design, design.Id == _selected, design.Id == _hovered, () =>
            {
                _selected = design.Id;
                RebuildDesigns();
                RebuildInfo();
            }, hovered =>
            {
                _hovered = hovered ? design.Id : null;
            }));
        }
    }

    /// <summary>Карточка чертежа: фон rgba(255,255,255,0.03), наведение 0.08, выбор rgba(80,140,255,0.25), рамка 0.08.</summary>
    private sealed class DesignCard : BaseButton
    {
        private readonly bool _selected;

        public DesignCard(FissionRodDesign design, bool selected, bool hovered, Action onSelect, Action<bool> onHover)
        {
            _selected = selected;
            MouseFilter = MouseFilterMode.Stop;
            HorizontalExpand = true;
            var box = Tgui.VBox(2,
                Tgui.Text(design.Name, Tgui.TextColor, 12, true),
                Tgui.Paragraph(design.Description, Tgui.Label));
            box.Margin = new Thickness(8);
            AddChild(box);
            OnPressed += _ => onSelect();
            OnMouseEntered += _ => onHover(true);
            OnMouseExited += _ => onHover(false);
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var rect = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
            var fill = _selected
                ? new Color(80, 140, 255, 64)
                : DrawMode == DrawModeEnum.Hover ? Color.White.WithAlpha(0.08f) : Color.White.WithAlpha(0.03f);
            handle.DrawRect(rect, fill);
            handle.DrawRect(rect, Color.White.WithAlpha(0.08f), false);
        }
    }

    #endregion

    #region Сведения о стержне

    private static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private void RebuildInfo()
    {
        _info.RemoveAllChildren();
        var design = _state?.Designs.FirstOrDefault(d => d.Id == _selected);
        if (design == null)
        {
            _info.AddChild(Tgui.NoticeBox(Loc.GetString("fission-fabricator-ui-select")));
            return;
        }

        // Table из tgui: строки «подпись — значение»; длинные значения переносятся, а не наезжают на соседей.
        var table = Tgui.VBox(2);
        void Row(string label, string value)
        {
            var name = Tgui.Paragraph($"[bold]{Loc.GetString(label)}[/bold]");
            name.HorizontalExpand = false;
            name.MinWidth = 230;
            name.MaxWidth = 230;
            var text = Tgui.Paragraph(FormattedMessage.EscapeText(value));
            text.HorizontalExpand = true;
            table.AddChild(Tgui.HBox(12, name, text));
        }

        void Spacer()
        {
            table.AddChild(new Control { MinHeight = 8 });
        }

        Row("fission-fabricator-ui-power", $"{Num(design.PowerAmount / 1000)} KW");
        Row("fission-fabricator-ui-power-amp", Num(design.PowerAmpMod));
        Spacer();
        Row("fission-fabricator-ui-heat", $"{Num(design.HeatAmount)} joules");
        Row("fission-fabricator-ui-heat-amp", Num(design.HeatAmpMod));
        Spacer();
        Row("fission-fabricator-ui-lifespan", design.MaxDurability < 0
            ? Loc.GetString("fission-fabricator-ui-infinite")
            : $"{Num(design.MaxDurability)} cycles");
        if (design.HeatEnrichment != null)
        {
            Spacer();
            Row("fission-fabricator-ui-heat-enrichment", design.HeatEnrichment);
            Row("fission-fabricator-ui-heat-enrichment-req", Num(design.HeatEnrichmentRequirement));
        }

        if (design.PowerEnrichment != null)
        {
            Spacer();
            Row("fission-fabricator-ui-power-enrichment", design.PowerEnrichment);
            Row("fission-fabricator-ui-power-enrichment-req", Num(design.PowerEnrichmentRequirement));
        }

        var requirements = Tgui.VBox(0);
        requirements.Margin = new Thickness(24, 0, 0, 0);
        if (design.NeighborRequirements.Count == 0)
            requirements.AddChild(Tgui.Text(Loc.GetString("fission-fabricator-ui-none-req")));
        foreach (var requirement in design.NeighborRequirements)
            requirements.AddChild(Tgui.Text(requirement));

        _info.AddChild(Tgui.MakeSection(design.Name, null, Tgui.VBox(4,
            table,
            Tgui.Text(Loc.GetString("fission-fabricator-ui-neighbors"), Tgui.TextColor, 12, true),
            requirements)));

        _info.AddChild(Divider());

        Control materials;
        if (design.Materials.Count == 0)
        {
            materials = Tgui.Text(Loc.GetString("fission-fabricator-ui-no-materials"), Tgui.Average);
        }
        else
        {
            var grid = new GridContainer { Columns = 3, HSeparationOverride = 12, VSeparationOverride = 2 };
            foreach (var material in design.Materials)
            {
                var available = _state?.Resources.FirstOrDefault(r => r.Id == material.Id)?.Amount ?? 0;
                var color = available >= material.Amount ? Tgui.TextColor : Tgui.Red;
                grid.AddChild(Tgui.Text(material.Name, color, 12, true));
                grid.AddChild(Tgui.Text(material.Amount.ToString(CultureInfo.InvariantCulture), color));
                grid.AddChild(Tgui.Text(Loc.GetString("fission-fabricator-ui-sheets", ("sheets", material.Sheets)), color));
            }

            materials = grid;
        }

        _info.AddChild(Tgui.MakeSection(Loc.GetString("fission-fabricator-ui-required"), null, materials));
        _info.AddChild(Divider());

        var id = design.Id;
        _info.AddChild(Tgui.Button(Loc.GetString("fission-fabricator-ui-fabricate"), () => OnFabricate?.Invoke(id),
            Tgui.Good, icon: "wrench"));
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

    #endregion

    #region Материалы

    private void RebuildStorage()
    {
        _storage.RemoveAllChildren();
        if (_state == null || _state.Resources.Count == 0)
        {
            _storage.AddChild(Tgui.Text(Loc.GetString("fission-fabricator-ui-empty"), Tgui.Average));
            return;
        }

        var grid = new GridContainer { Columns = 4, HSeparationOverride = 12, VSeparationOverride = 4 };
        foreach (var resource in _state.Resources)
        {
            var id = resource.Id;
            grid.AddChild(Tgui.Text(resource.Name, Tgui.TextColor, 12, true));
            grid.AddChild(Tgui.Text(Loc.GetString("fission-fabricator-ui-units", ("amount", resource.Amount))));
            grid.AddChild(Tgui.Text(Loc.GetString("fission-fabricator-ui-sheets", ("sheets", resource.Sheets))));

            var buttons = Tgui.HBox(2);
            var custom = new LineEdit { Visible = false, MinWidth = 50, PlaceHolder = "1-" + resource.Sheets };
            custom.OnTextEntered += args =>
            {
                custom.Visible = false;
                if (int.TryParse(args.Text, out var sheets))
                    OnEject?.Invoke(id, Math.Clamp(sheets, 1, resource.Sheets));
            };

            buttons.AddChild(Tgui.Button("1", () => OnEject?.Invoke(id, 1)));
            buttons.AddChild(Tgui.Button("C", () =>
            {
                custom.Visible = !custom.Visible;
                if (custom.Visible)
                    custom.GrabKeyboardFocus();
            }));
            if (resource.Sheets >= 5)
                buttons.AddChild(Tgui.Button("5", () => OnEject?.Invoke(id, 5)));
            buttons.AddChild(Tgui.Button(Loc.GetString("fission-fabricator-ui-all"), () => OnEject?.Invoke(id, resource.Sheets)));
            buttons.AddChild(custom);
            grid.AddChild(buttons);
        }

        _storage.AddChild(grid);
    }

    #endregion
}
