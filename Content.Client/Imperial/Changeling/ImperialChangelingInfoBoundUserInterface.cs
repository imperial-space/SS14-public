using System.Numerics;
using Content.Client.Message;
using Content.Shared.Imperial.Changeling;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Changeling;

/// <summary>
/// tgui AntagInfoChangeling.tsx: вступление с задачами, справка по способностям, предупреждение о предательстве,
/// украденные воспоминания и дополнительная украденная информация.
/// </summary>
[UsedImplicitly]
public sealed class ImperialChangelingInfoBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label _dnaLabel = default!;
    private RichTextLabel _intro = default!;
    private BoxContainer _objectives = default!;
    private RichTextLabel _abilitiesTrueName = default!;
    private BoxContainer _memoriesBody = default!;
    private BoxContainer _stolenBody = default!;
    private ImperialChangelingInfoState? _state;
    private string? _selectedMemory;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-changeling-info-title");
        _window.SetSize = new Vector2(720, 750);
        Build(_window);
    }

    private void Build(DefaultWindow window)
    {
        var background = new PanelContainer
        {
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = TguiStyle.WindowBackground },
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        window.Contents.AddChild(background);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(4) };
        background.AddChild(root);

        // IntroductionSection
        var intro = TguiStyle.Section(out var introTitle, out var introBody);
        intro.MinHeight = 190;
        intro.MaxHeight = 190;
        introTitle.AddChild(TguiStyle.Title(Loc.GetString("imperial-changeling-info-intro")));
        introTitle.AddChild(new Control { HorizontalExpand = true });
        // <Button icon="dna" color="purple">{absorbed_dna}</Button>
        var dnaBox = new PanelContainer
        {
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = TguiStyle.Purple },
            ToolTip = Loc.GetString("imperial-changeling-info-absorbed-dna"),
            MouseFilter = Control.MouseFilterMode.Stop,
            VerticalAlignment = Control.VAlignment.Center,
        };
        var dnaRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(6, 2), SeparationOverride = 4 };
        dnaRow.AddChild(TguiStyle.Icon(EntMan.System<Robust.Client.GameObjects.SpriteSystem>().Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Changeling/hud.rsi"), "extract_dna_sting")), 20));
        _dnaLabel = new Label();
        dnaRow.AddChild(_dnaLabel);
        dnaBox.AddChild(dnaRow);
        introTitle.AddChild(dnaBox);
        var introScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinHeight = 130 };
        var introColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        _intro = new RichTextLabel();
        introColumn.AddChild(_intro);
        _objectives = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        introColumn.AddChild(_objectives);
        introScroll.AddChild(introColumn);
        introBody.AddChild(introScroll);
        root.AddChild(intro);

        // AbilitiesSection: три колонки по два абзаца.
        var abilities = TguiStyle.Section(out var abilitiesTitle, out var abilitiesBody);
        abilities.VerticalExpand = true;
        abilities.SizeFlagsStretchRatio = 4;
        abilitiesTitle.AddChild(TguiStyle.Title(Loc.GetString("imperial-changeling-info-abilities")));
        var abilityColumns = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var abilityScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinHeight = 160 };
        abilityScroll.AddChild(abilityColumns);
        abilitiesBody.AddChild(abilityScroll);
        for (var column = 0; column < 3; column++)
        {
            if (column > 0)
                abilityColumns.AddChild(Divider(vertical: true));

            // Три равные колонки: без фиксированной ширины текст растягивает первую.
            var stack = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SetWidth = 220, SeparationOverride = 6 };
            for (var paragraph = 0; paragraph < 2; paragraph++)
            {
                if (paragraph > 0)
                    stack.AddChild(Divider(vertical: false));

                var label = new RichTextLabel { VerticalExpand = true };
                var key = $"imperial-changeling-info-ability-{column * 2 + paragraph + 1}";
                if (column == 2 && paragraph == 1)
                    _abilitiesTrueName = label;
                else
                    label.SetMarkup($"[color=#7e90a7]{Loc.GetString(key)}[/color]");
                stack.AddChild(label);
            }

            abilityColumns.AddChild(stack);
        }

        root.AddChild(abilities);

        // BetrayalWarning
        root.AddChild(TguiStyle.NoticeBox(Loc.GetString("imperial-changeling-info-betrayal"), danger: true));

        // MemoriesSection и VictimPatternsSection
        var bottom = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, VerticalExpand = true, SizeFlagsStretchRatio = 3, SeparationOverride = 6, MinHeight = 180 };
        var memories = TguiStyle.Section(out var memoriesTitle, out _memoriesBody);
        memories.HorizontalExpand = true;
        memoriesTitle.AddChild(TguiStyle.Title(Loc.GetString("imperial-changeling-info-memories-title")));
        memoriesTitle.AddChild(new Control { HorizontalExpand = true });
        var infoButton = new Button { Text = "i", ToolTip = Loc.GetString("imperial-changeling-info-memories-tip") };
        TguiStyle.Paint(infoButton, TguiStyle.Default);
        memoriesTitle.AddChild(infoButton);
        bottom.AddChild(memories);

        var stolen = TguiStyle.Section(out var stolenTitle, out _stolenBody);
        stolen.HorizontalExpand = true;
        stolenTitle.AddChild(TguiStyle.Title(Loc.GetString("imperial-changeling-info-stolen-title")));
        bottom.AddChild(stolen);
        root.AddChild(bottom);
    }

    private static Control Divider(bool vertical)
    {
        return new PanelContainer
        {
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = Color.FromHex("#ffffff1a") },
            MinWidth = vertical ? 2 : 0,
            MinHeight = vertical ? 0 : 2,
            HorizontalExpand = !vertical,
            VerticalExpand = vertical,
        };
    }

    /// <summary>&lt;Dimmer fontSize="20px"&gt;Сначала поглотите жертву!&lt;/Dimmer&gt;</summary>
    private static Control Dimmer()
    {
        var label = new RichTextLabel { HorizontalAlignment = Control.HAlignment.Center, VerticalAlignment = Control.VAlignment.Center, VerticalExpand = true };
        label.SetMarkup($"[font size=20]{Loc.GetString("imperial-changeling-info-absorb-first")}[/font]");
        return label;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialChangelingInfoState info || _window == null)
            return;

        _state = info;

        _dnaLabel.Text = info.AbsorbedDna.ToString();

        // «Вы {true_name} из <span style={hivestyle}>{hive_name}</span>.»
        _intro.SetMarkup(Loc.GetString("imperial-changeling-info-you-are",
            ("name", FormattedMessage.EscapeText(info.TrueName)), ("hive", FormattedMessage.EscapeText(info.HiveName))));

        // ObjectivePrintout
        _objectives.RemoveAllChildren();
        var header = new RichTextLabel();
        header.SetMarkup(Loc.GetString("imperial-changeling-info-objectives"));
        _objectives.AddChild(header);
        if (info.Objectives.Count == 0)
        {
            var none = new RichTextLabel();
            none.SetMarkup(Loc.GetString("imperial-changeling-info-objectives-none"));
            _objectives.AddChild(none);
        }

        for (var i = 0; i < info.Objectives.Count; i++)
        {
            var objective = new RichTextLabel();
            objective.SetMarkup(Loc.GetString("imperial-changeling-info-objective", ("index", i + 1), ("text", FormattedMessage.EscapeText(info.Objectives[i]))));
            _objectives.AddChild(objective);
        }

        _abilitiesTrueName.SetMarkup($"[color=#7e90a7]{Loc.GetString("imperial-changeling-info-ability-6", ("name", FormattedMessage.EscapeText(info.TrueName)))}[/color]");

        UpdateMemories(info);

        _stolenBody.RemoveAllChildren();
        if (info.StolenAntagInfo.Length == 0)
        {
            _stolenBody.AddChild(Dimmer());
        }
        else
        {
            var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
            var label = new RichTextLabel();
            label.SetMessage(info.StolenAntagInfo);
            scroll.AddChild(label);
            _stolenBody.AddChild(scroll);
        }
    }

    private void UpdateMemories(ImperialChangelingInfoState info)
    {
        _memoriesBody.RemoveAllChildren();
        if (info.Memories.Count == 0)
        {
            _memoriesBody.AddChild(Dimmer());
            return;
        }

        _selectedMemory ??= info.Memories[0].Name;
        var dropdown = new OptionButton { HorizontalExpand = true };
        var selectedIndex = 0;
        for (var i = 0; i < info.Memories.Count; i++)
        {
            dropdown.AddItem(info.Memories[i].Name, i);
            if (info.Memories[i].Name == _selectedMemory)
                selectedIndex = i;
        }

        dropdown.SelectId(selectedIndex);
        dropdown.OnItemSelected += args =>
        {
            _selectedMemory = info.Memories[args.Id].Name;
            if (_state != null)
                UpdateMemories(_state);
        };
        _memoriesBody.AddChild(dropdown);

        var story = new RichTextLabel { Margin = new Thickness(0, 6, 0, 0) };
        story.SetMessage(info.Memories[selectedIndex].Story);
        _memoriesBody.AddChild(story);
    }
}
