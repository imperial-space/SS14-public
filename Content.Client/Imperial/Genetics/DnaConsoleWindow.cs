using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Genetics;

/// <summary>
/// РљРѕРЅСЃРѕР»СЊ Р”РќРљ, РїРѕРІС‚РѕСЂСЏСЋС‰Р°СЏ tgui-РёРЅС‚РµСЂС„РµР№СЃ SS13 (DnaConsole): СЃРєР°РЅРµСЂ, СЂРµР¶РёРјС‹ В«РҐСЂР°РЅРёР»РёС‰РµВ», В«РЎРµРєРІРµРЅСЃРѕСЂВ»,
/// В«Р¤РµСЂРјРµРЅС‚С‹В», В«РћСЃРѕР±РµРЅРЅРѕСЃС‚РёВ», С…СЂР°РЅРёР»РёС‰Р° РєРѕРЅСЃРѕР»Рё, РґРёСЃРєРµС‚С‹ Рё РїСЂРѕРґРІРёРЅСѓС‚С‹С… РёРЅР¶РµРєС‚РѕСЂРѕРІ, Р±СѓС„РµСЂС‹ РіРµРЅРµС‚РёС‡РµСЃРєРѕРіРѕ РѕР±Р»РёРєР°.
/// </summary>
public sealed class DnaConsoleWindow : DefaultWindow
{
    private enum ConsoleMode { Storage, Sequencer, Enzymes, Features }
    private enum StorageMode { Console, Disk, Injector }

    // РџР°Р»РёС‚СЂР° tgui.
    private static readonly Color Good = Color.FromHex("#5baa27");
    private static readonly Color Average = Color.FromHex("#f08f11");
    private static readonly Color Bad = Color.FromHex("#db2828");
    private static readonly Color LabelColor = Color.FromHex("#8b9bb0");
    private static readonly Color Olive = Color.FromHex("#9a9d00");
    private static readonly Color GeneGreen = Color.FromHex("#20b142");
    private static readonly Color GeneBlue = Color.FromHex("#2185d0");
    private static readonly Color GeneGrey = Color.FromHex("#646464");
    private static readonly Color Purple = Color.FromHex("#8b48bb");
    private static readonly Color SectionBack = Color.FromHex("#131313");
    private static readonly Color SectionTitle = Color.FromHex("#4972a1");
    private static readonly ResPath GenesRsi = new("/Textures/Imperial/Genetics/dna_genes.rsi");
    private static readonly ChromosomeKind[] ChromosomeKinds =
        { ChromosomeKind.Stabilizer, ChromosomeKind.Synchronizer, ChromosomeKind.Power, ChromosomeKind.Energy };

    [Dependency] private readonly IInputManager _input = default!;

    public event Action<BoundUserInterfaceMessage>? OnMessage;

    private readonly BoxContainer _root = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private DnaConsoleBoundUserInterfaceState? _state;

    // РЎРѕСЃС‚РѕСЏРЅРёРµ РІРёРґР° (tgui_view_state).
    private ConsoleMode _mode = ConsoleMode.Storage;
    private StorageMode _storageMode = StorageMode.Console;
    private bool _consoleChromosomes;
    private bool _diskEnzymes;
    private ProtoId<GeneticMutationPrototype>? _sequencerMutation;
    private bool _jokerActive;
    private readonly Dictionary<string, int> _storageSelection = new();
    private ChromosomeKind? _selectedChromo;
    private readonly HashSet<string> _expanded = new();
    private string _newInjectorName = string.Empty;

    public DnaConsoleWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("dna-console-title");
        MinSize = SetSize = new Vector2(550, 710);
        Contents.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _root } });
    }

    private void Send(BoundUserInterfaceMessage message)
    {
        OnMessage?.Invoke(message);
    }

    public void UpdateState(DnaConsoleBoundUserInterfaceState state)
    {
        _state = state;
        if (_mode == ConsoleMode.Sequencer && !state.IsViableSubject)
            _mode = ConsoleMode.Storage;
        if (_storageMode == StorageMode.Disk && !state.HasDisk)
            _storageMode = StorageMode.Console;

        Rebuild();
    }

    private void Rebuild()
    {
        _root.RemoveAllChildren();
        if (_state == null)
            return;

        if (_state.IsPulsing)
        {
            _root.AddChild(Section(null, null, Centered(Loc.GetString("dna-console-pulsing", ("seconds", _state.TimeToPulse)), Average)));
        }

        _root.AddChild(BuildScanner());
        _root.AddChild(BuildCommands());

        switch (_mode)
        {
            case ConsoleMode.Storage:
                _root.AddChild(BuildStorage());
                break;
            case ConsoleMode.Sequencer:
                BuildSequencer();
                break;
            case ConsoleMode.Enzymes:
                BuildEnzymes(_state.SubjectUniqueIdentity, false);
                break;
            case ConsoleMode.Features:
                BuildEnzymes(_state.SubjectUniqueFeatures, true);
                break;
        }
    }

    #region РЎС‚СЂРѕРёС‚РµР»СЊРЅС‹Рµ Р±Р»РѕРєРё

    private static Control Section(string? title, Control? buttons, Control content, float minHeight = 0)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(6) };
        if (title != null || buttons != null)
        {
            var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            header.AddChild(new Label { Text = title ?? string.Empty, StyleClasses = { "LabelHeading" }, HorizontalExpand = true });
            if (buttons != null)
                header.AddChild(buttons);
            box.AddChild(header);
            box.AddChild(new PanelContainer
            {
                MinHeight = 2,
                PanelOverride = new StyleBoxFlat { BackgroundColor = SectionTitle },
            });
        }

        box.AddChild(content);
        return new PanelContainer
        {
            MinHeight = minHeight,
            PanelOverride = new StyleBoxFlat { BackgroundColor = SectionBack },
            Children = { box },
        };
    }

    private static BoxContainer HBox(params Control[] children)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        foreach (var child in children)
        {
            box.AddChild(child);
        }

        return box;
    }

    private static BoxContainer VBox(params Control[] children)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        foreach (var child in children)
        {
            box.AddChild(child);
        }

        return box;
    }

    private static Control LabeledItem(string label, Control value)
    {
        return HBox(new Label { Text = label, FontColorOverride = LabelColor, MinWidth = 120 }, value);
    }

    private static Control LabeledText(string label, string value, Color? color = null)
    {
        return LabeledItem(label, new Label { Text = value, FontColorOverride = color, HorizontalExpand = true, ClipText = false });
    }

    private static Label Text(string text, Color? color = null)
    {
        return new Label { Text = text, FontColorOverride = color };
    }

    private static Control Centered(string text, Color color)
    {
        return new Label { Text = text, FontColorOverride = color, HorizontalAlignment = HAlignment.Center };
    }

    private static Control Divider()
    {
        return new PanelContainer { MinHeight = 1, Margin = new Thickness(0, 4), PanelOverride = new StyleBoxFlat { BackgroundColor = LabelColor.WithAlpha(0.3f) } };
    }

    private Button MakeButton(string text, Action onPressed, bool disabled = false, Color? color = null, bool selected = false)
    {
        var button = new Button { Text = text, Disabled = disabled, ToggleMode = selected, Pressed = selected };
        if (color is { } tint)
            button.ModulateSelfOverride = tint;

        button.OnPressed += _ => onPressed();
        return button;
    }

    private static Control ProgressBarWithText(float value, float max, Color color, string text)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = max,
            Value = Math.Clamp(value, 0, max),
            MinHeight = 20,
            HorizontalExpand = true,
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color },
        };

        return new Control
        {
            HorizontalExpand = true,
            Children = { bar, new Label { Text = text, HorizontalAlignment = HAlignment.Center } },
        };
    }

    private static Control Dropdown(string placeholder, IReadOnlyList<string> options, Action<int> onSelected, bool disabled = false)
    {
        var button = new OptionButton { MinWidth = 240, Disabled = disabled || options.Count == 0 };
        button.AddItem(placeholder, -1);
        for (var i = 0; i < options.Count; i++)
        {
            button.AddItem(options[i], i);
        }

        button.OnItemSelected += args =>
        {
            if (args.Id >= 0)
                onSelected(args.Id);
            button.SelectId(-1);
        };
        return button;
    }

    private static Color QualityColor(MutationQuality quality)
    {
        return quality switch
        {
            MutationQuality.Positive => Good,
            MutationQuality.Negative => Bad,
            _ => Average,
        };
    }

    private static string ChromoName(ChromosomeKind kind)
    {
        return Loc.GetString($"dna-chromosome-{kind.ToString().ToLowerInvariant()}");
    }

    private static string ChromoList(ChromosomeKind kinds)
    {
        return string.Join(", ", ChromosomeKinds.Where(k => (kinds & k) != 0).Select(ChromoName));
    }

    #endregion

    #region РЎРєР°РЅРµСЂ Р”РќРљ

    private Control BuildScanner()
    {
        var state = _state!;
        var buttons = HBox();
        if (!state.IsScannerConnected)
        {
            buttons.AddChild(MakeButton(Loc.GetString("dna-console-connect-scanner"), () => { }));
        }
        else
        {
            if (state.HasDelayedAction)
                buttons.AddChild(MakeButton(Loc.GetString("dna-console-cancel-delay"), () => Send(new DnaConsoleCancelDelayMessage())));

            if (state.IsViableSubject)
            {
                var scramble = Loc.GetString("dna-console-scramble");
                if (!state.IsScrambleReady)
                    scramble += $" ({state.ScrambleSeconds}СЃ)";
                buttons.AddChild(MakeButton(scramble, () => Send(new DnaConsoleScrambleMessage()), !state.IsScrambleReady || state.IsPulsing));
            }

            buttons.AddChild(MakeButton(Loc.GetString(state.ScannerLocked ? "dna-console-locked" : "dna-console-unlocked"),
                () => Send(new DnaConsoleToggleLockMessage()), false, state.ScannerLocked ? Bad : null));
            buttons.AddChild(MakeButton(Loc.GetString(state.ScannerOpen ? "dna-console-close" : "dna-console-open"),
                () => Send(new DnaConsoleToggleDoorMessage()), state.ScannerLocked || state.ScannerOpen));
        }

        Control content;
        if (!state.IsScannerConnected)
        {
            content = Text(Loc.GetString("dna-console-scanner-not-connected"), Bad);
        }
        else if (!state.IsViableSubject)
        {
            content = Text(Loc.GetString("dna-console-no-viable-subject"), Average);
        }
        else
        {
            var (statusText, statusColor) = state.SubjectStatus switch
            {
                DnaSubjectStatus.Conscious => ("dna-console-status-conscious", Good),
                DnaSubjectStatus.SoftCrit => ("dna-console-status-critical", Average),
                DnaSubjectStatus.Dead => ("dna-console-status-dead", Bad),
                DnaSubjectStatus.Transforming => ("dna-console-status-transforming", Bad),
                _ => ("dna-console-status-unconscious", Average),
            };

            var healthColor = state.SubjectHealth > 100 ? Olive : state.SubjectHealth >= 70 ? Good : state.SubjectHealth >= 30 ? Average : Bad;
            var damageColor = state.SubjectDamage > 71 ? Bad : state.SubjectDamage >= 30 ? Average : state.SubjectDamage > 0 ? Good : Olive;

            content = VBox(
                LabeledItem(Loc.GetString("dna-console-label-status"),
                    HBox(Text(state.SubjectName), Text("в†’", LabelColor), Text(Loc.GetString(statusText), statusColor))),
                LabeledItem(Loc.GetString("dna-console-label-health"),
                    ProgressBarWithText(state.SubjectHealth, 100, healthColor, $"{state.SubjectHealth}%")),
                LabeledItem(Loc.GetString("dna-console-label-genetic-damage"),
                    ProgressBarWithText(state.SubjectDamage, 100, damageColor, $"{state.SubjectDamage}%")));
        }

        return Section(Loc.GetString("dna-console-scanner-title"), buttons, content);
    }

    #endregion

    #region РљРѕРЅСЃРѕР»СЊ Р”РќРљ

    private Control BuildCommands()
    {
        var state = _state!;
        Control? buttons = state.IsInjectorReady
            ? null
            : Text(Loc.GetString("dna-console-injector-cooldown", ("seconds", state.InjectorSeconds)), LabelColor);

        var modes = HBox(
            MakeButton(Loc.GetString("dna-console-mode-storage"), () => SetMode(ConsoleMode.Storage), false, null, _mode == ConsoleMode.Storage),
            MakeButton(Loc.GetString("dna-console-mode-sequencer"), () => SetMode(ConsoleMode.Sequencer), !state.IsViableSubject, null, _mode == ConsoleMode.Sequencer),
            MakeButton(Loc.GetString("dna-console-mode-enzymes"), () => SetMode(ConsoleMode.Enzymes), false, null, _mode == ConsoleMode.Enzymes),
            MakeButton(Loc.GetString("dna-console-mode-features"), () => SetMode(ConsoleMode.Features), false, null, _mode == ConsoleMode.Features));

        var content = VBox(LabeledItem(Loc.GetString("dna-console-label-mode"), modes));
        if (state.HasDisk)
        {
            content.AddChild(LabeledItem(Loc.GetString("dna-console-label-disk"), MakeButton(Loc.GetString("dna-console-eject"), () =>
            {
                _storageMode = StorageMode.Console;
                Send(new DnaConsoleEjectDiskMessage());
            })));
        }

        return Section(Loc.GetString("dna-console-title"), buttons, content);
    }

    private void SetMode(ConsoleMode mode)
    {
        _mode = mode;
        Rebuild();
    }

    #endregion

    #region РЎРµРєРІРµРЅСЃРѕСЂ

    private void BuildSequencer()
    {
        var state = _state!;
        var mutations = state.Occupant;
        var mutation = mutations.FirstOrDefault(m => m.Mutation == _sequencerMutation);

        var genes = new GridContainer { Columns = 2 };
        foreach (var entry in mutations)
        {
            genes.AddChild(GenomeImage(entry, entry.Mutation == _sequencerMutation));
        }

        var top = HBox(
            new Control
            {
                MinWidth = mutations.Count <= 8 ? 154 : 174,
                Children = { Section(Loc.GetString("dna-console-sequences"), null, new ScrollContainer { HScrollEnabled = false, MinHeight = 170, Children = { genes } }, 214) },
            },
            new Control
            {
                HorizontalExpand = true,
                Children = { Section(Loc.GetString("dna-console-sequence-info"), null, MutationInfo(mutation), 214) },
            });
        _root.AddChild(top);

        if (state.SubjectStatus == DnaSubjectStatus.Dead)
        {
            _root.AddChild(Section(null, null, Text(Loc.GetString("dna-console-corrupted-deceased"), Bad)));
            return;
        }

        if (state.IsMonkey && mutation?.Mutation.Id != "MutationMonkified")
        {
            _root.AddChild(Section(null, null, Text(Loc.GetString("dna-console-corrupted-monkey"), Bad)));
            return;
        }

        if (state.SubjectStatus == DnaSubjectStatus.Transforming)
        {
            _root.AddChild(Section(null, null, Text(Loc.GetString("dna-console-corrupted-transforming"), Bad)));
            return;
        }

        Control jokerButtons;
        if (!state.IsJokerReady)
        {
            jokerButtons = Text(Loc.GetString("dna-console-joker-cooldown", ("seconds", state.JokerSeconds)), LabelColor);
        }
        else if (_jokerActive)
        {
            jokerButtons = HBox(Text(Loc.GetString("dna-console-joker-hint"), LabelColor),
                MakeButton(Loc.GetString("dna-console-joker-cancel"), () => { _jokerActive = false; Rebuild(); }));
        }
        else
        {
            jokerButtons = MakeButton(Loc.GetString("dna-console-joker-use"), () => { _jokerActive = true; Rebuild(); }, false, Purple);
        }

        _root.AddChild(Section(Loc.GetString("dna-console-genome-sequencer"), jokerButtons, GenomeSequencer(mutation)));
    }

    private Control GenomeImage(DnaMutationState mutation, bool selected)
    {
        var state = mutation.Class != DnaMutationClass.Normal ? "extra" : mutation.Discovered ? "discovered" : "undiscovered";
        var image = new AnimatedTextureRect { MinSize = new Vector2(64, 37) };
        image.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(GenesRsi, state));
        image.DisplayRect.Stretch = TextureRect.StretchMode.KeepAspectCentered;

        var button = new ContainerButton { Margin = new Thickness(2), ToolTip = mutation.Discovered ? mutation.Name : mutation.Alias };
        button.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BorderColor = selected ? Color.FromHex("#22aa00") : Color.Transparent,
                BorderThickness = new Thickness(2),
            },
            Children = { image },
        });
        button.OnPressed += _ =>
        {
            _sequencerMutation = mutation.Mutation;
            Rebuild();
        };
        return button;
    }

    private Control GenomeSequencer(DnaMutationState? mutation)
    {
        if (mutation == null)
            return Text(Loc.GetString("dna-console-no-genome-selected"), Average);

        var sequence = mutation.Sequence;
        var pairs = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 2 };
        for (var i = 0; i < sequence.Length; i += 2)
        {
            if (i % 8 == 0 && i != 0)
            {
                pairs.AddChild(new PanelContainer
                {
                    MinSize = new Vector2(8, 2),
                    VerticalAlignment = VAlignment.Center,
                    PanelOverride = new StyleBoxFlat { BackgroundColor = LabelColor },
                });
            }

            var matched = IsPairMatched(sequence, i);
            pairs.AddChild(VBox(
                GeneButton(mutation, i),
                new PanelContainer
                {
                    MinSize = new Vector2(3, 8),
                    HorizontalAlignment = HAlignment.Center,
                    PanelOverride = new StyleBoxFlat { BackgroundColor = matched ? LabelColor : Bad },
                },
                GeneButton(mutation, i + 1)));
        }

        return VBox(pairs, Text(Loc.GetString("dna-console-sequencer-tip"), LabelColor));
    }

    private Control GeneButton(DnaMutationState mutation, int index)
    {
        var gene = index < mutation.Sequence.Length ? mutation.Sequence[index] : 'X';
        var disabled = mutation.Class != DnaMutationClass.Normal;
        var color = disabled ? GeneGrey : gene switch
        {
            'A' or 'T' => GeneGreen,
            'G' or 'C' => GeneBlue,
            _ => GeneGrey,
        };

        var button = new Button
        {
            Text = gene.ToString(),
            MinSize = new Vector2(22, 22),
            Disabled = disabled,
            ModulateSelfOverride = color,
            EnableAllKeybinds = true,
        };

        button.OnPressed += args =>
        {
            DnaGeneAction action;
            if (args.Event.Function == EngineKeyFunctions.UIRightClick)
                action = DnaGeneAction.Prev;
            else if (_input.IsKeyDown(Keyboard.Key.Control))
                action = DnaGeneAction.Clear;
            else
                action = DnaGeneAction.Next;

            var joker = _jokerActive && action == DnaGeneAction.Next;
            if (joker)
                _jokerActive = false;

            Send(new DnaConsolePulseGeneMessage(mutation.Mutation, index, action, joker));
        };

        // РћСЂР°РЅР¶РµРІР°СЏ СЂР°РјРєР° РЅР° РёР·РЅР°С‡Р°Р»СЊРЅРѕ РЅРµРёР·РІРµСЃС‚РЅС‹С… РїРѕР·РёС†РёСЏС….
        var unknownByDefault = index < mutation.DefaultSequence.Length && mutation.DefaultSequence[index] == 'X' && !mutation.Active;
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BorderColor = unknownByDefault ? Average : Color.Transparent,
                BorderThickness = new Thickness(1),
            },
            Children = { button },
        };
    }

    private static bool IsPairMatched(string sequence, int index)
    {
        if (index + 1 >= sequence.Length)
            return false;

        var a = sequence[index];
        var b = sequence[index + 1];
        return a == 'A' && b == 'T' || a == 'T' && b == 'A' || a == 'G' && b == 'C' || a == 'C' && b == 'G';
    }

    #endregion

    #region РРЅС„РѕСЂРјР°С†РёСЏ Рѕ РјСѓС‚Р°С†РёРё

    private Control MutationInfo(DnaMutationState? mutation)
    {
        var state = _state!;
        if (mutation == null)
            return Text(Loc.GetString("dna-console-nothing"), LabelColor);

        var source = mutation.Ref.Source;
        if (source == DnaMutationSource.Occupant && !mutation.Discovered)
            return LabeledText(Loc.GetString("dna-console-label-name"), mutation.Alias);

        var box = VBox(
            LabeledText(Loc.GetString("dna-console-label-name"), mutation.Name, QualityColor(mutation.Quality)),
            LabeledItem(Loc.GetString("dna-console-label-description"),
                new RichTextLabel { Text = mutation.Description, HorizontalExpand = true }),
            LabeledText(Loc.GetString("dna-console-label-instability"), mutation.Instability.ToString()),
            Divider());

        var savedToConsole = state.Console.Any(m => m.Mutation == mutation.Mutation && m.AppliedChromo == mutation.AppliedChromo);
        var savedToDisk = state.Disk.Any(m => m.Mutation == mutation.Mutation && m.AppliedChromo == mutation.AppliedChromo);

        if (source is DnaMutationSource.Console or DnaMutationSource.Disk)
        {
            var combinable = state.Disk.Concat(state.Console)
                .GroupBy(m => m.Name).Select(g => g.First())
                .Where(m => m.Name != mutation.Name)
                .ToList();
            box.AddChild(Dropdown(Loc.GetString("dna-console-combine"), combinable.Select(m => m.Name).ToList(),
                i => Send(new DnaConsoleCombineMessage(combinable[i].Ref, mutation.Ref)),
                source == DnaMutationSource.Disk && (!state.HasDisk || state.DiskCapacity <= 0 || state.DiskReadOnly)));
        }

        if (source is DnaMutationSource.Occupant or DnaMutationSource.Console or DnaMutationSource.Disk)
        {
            var injectors = state.Injectors.Select(i => i.Name).ToList();
            box.AddChild(Dropdown(Loc.GetString("dna-console-add-advinj"), injectors,
                i => Send(new DnaConsoleAddAdvInjMessage(mutation.Ref, injectors[i])), !mutation.Active));

            var other = mutation.Class == DnaMutationClass.Other;
            box.AddChild(HBox(
                MakeButton(Loc.GetString("dna-console-print-activator"),
                    () => Send(new DnaConsolePrintInjectorMessage(mutation.Ref, true)), !state.IsInjectorReady || !mutation.Active || other),
                MakeButton(Loc.GetString("dna-console-print-mutator"),
                    () => Send(new DnaConsolePrintInjectorMessage(mutation.Ref, false)), !state.IsInjectorReady || !mutation.Active || other),
                MakeButton(Loc.GetString("dna-console-crispr", ("charges", state.CrisprCharges)), () => { }, true)));
        }

        var actions = HBox();
        if (source is DnaMutationSource.Disk or DnaMutationSource.Occupant)
        {
            actions.AddChild(MakeButton(Loc.GetString("dna-console-save-console"),
                () => Send(new DnaConsoleSaveConsoleMessage(mutation.Ref)),
                savedToConsole || !mutation.Active || mutation.Class == DnaMutationClass.Other));
        }

        if (source is DnaMutationSource.Console or DnaMutationSource.Occupant)
        {
            actions.AddChild(MakeButton(Loc.GetString("dna-console-save-disk"),
                () => Send(new DnaConsoleSaveDiskMessage(mutation.Ref)),
                savedToDisk || !state.HasDisk || state.DiskCapacity <= 0 || state.DiskReadOnly || !mutation.Active
                || mutation.Class == DnaMutationClass.Other));
        }

        if (source is DnaMutationSource.Console or DnaMutationSource.Disk or DnaMutationSource.Injector)
        {
            actions.AddChild(MakeButton(Loc.GetString($"dna-console-delete-{source.ToString().ToLowerInvariant()}"),
                () => Send(new DnaConsoleDeleteMutationMessage(mutation.Ref)), false, Bad));
        }

        if (mutation.Class == DnaMutationClass.Extra)
            actions.AddChild(MakeButton(Loc.GetString("dna-console-nullify"), () => Send(new DnaConsoleNullifyMessage(mutation.Mutation))));

        box.AddChild(actions);
        box.AddChild(Divider());
        box.AddChild(ChromosomeInfo(mutation, source != DnaMutationSource.Occupant));
        return box;
    }

    private Control ChromosomeInfo(DnaMutationState mutation, bool disabled)
    {
        switch (mutation.CanChromo)
        {
            case DnaChromosomeState.Never:
                return Text(Loc.GetString("dna-console-chromo-never"), LabelColor);
            case DnaChromosomeState.Used:
                return Text(Loc.GetString("dna-console-chromo-applied", ("chromosome", ChromoName(mutation.AppliedChromo))), LabelColor);
        }

        if (disabled)
            return Text(Loc.GetString("dna-console-chromo-none"), LabelColor);

        var valid = ChromosomeKinds
            .Where(k => (mutation.ValidChromos & k) != 0 && _state!.Chromosomes.GetValueOrDefault(k) > 0)
            .ToList();
        return VBox(
            Dropdown(Loc.GetString(valid.Count == 0 ? "dna-console-chromo-no-suitable" : "dna-console-chromo-select"),
                valid.Select(ChromoName).ToList(),
                i => Send(new DnaConsoleApplyChromoMessage(mutation.Ref, valid[i]))),
            Text(Loc.GetString("dna-console-chromo-compatible", ("list", ChromoList(mutation.ValidChromos))), LabelColor));
    }

    #endregion

    #region РҐСЂР°РЅРёР»РёС‰Рµ

    private Control BuildStorage()
    {
        var state = _state!;
        var buttons = HBox();
        if (_storageMode == StorageMode.Console)
        {
            buttons.AddChild(MakeButton(Loc.GetString("dna-console-storage-mutations"), () => { _consoleChromosomes = false; Rebuild(); }, false, null, !_consoleChromosomes));
            buttons.AddChild(MakeButton(Loc.GetString("dna-console-storage-chromosomes"), () => { _consoleChromosomes = true; Rebuild(); }, false, null, _consoleChromosomes));
        }
        else if (_storageMode == StorageMode.Disk)
        {
            buttons.AddChild(MakeButton(Loc.GetString("dna-console-storage-mutations"), () => { _diskEnzymes = false; Rebuild(); }, false, null, !_diskEnzymes));
            buttons.AddChild(MakeButton(Loc.GetString("dna-console-storage-enzymes"), () => { _diskEnzymes = true; Rebuild(); }, false, null, _diskEnzymes));
        }

        buttons.AddChild(new Control { MinWidth = 6 });
        buttons.AddChild(MakeButton(Loc.GetString("dna-console-storage-console"), () => { _storageMode = StorageMode.Console; _consoleChromosomes = false; Rebuild(); }, false, null, _storageMode == StorageMode.Console));
        buttons.AddChild(MakeButton(Loc.GetString("dna-console-storage-disk"), () => { _storageMode = StorageMode.Disk; _diskEnzymes = false; Rebuild(); }, !state.HasDisk, null, _storageMode == StorageMode.Disk));
        buttons.AddChild(MakeButton(Loc.GetString("dna-console-storage-advinj"), () => { _storageMode = StorageMode.Injector; Rebuild(); }, false, null, _storageMode == StorageMode.Injector));

        Control content = _storageMode switch
        {
            StorageMode.Console when _consoleChromosomes => StorageChromosomes(),
            StorageMode.Console => StorageMutations(state.Console, "console"),
            StorageMode.Disk when _diskEnzymes => VBox(
                GeneticMakeupInfo(state.DiskMakeup),
                MakeButton(Loc.GetString("dna-console-delete"), () => Send(new DnaConsoleDeleteDiskMakeupMessage()), state.DiskMakeup == null, Bad)),
            StorageMode.Disk => StorageMutations(state.Disk, "disk"),
            _ => AdvancedInjectors(),
        };

        return Section(Loc.GetString("dna-console-storage-title"), buttons, content, 300);
    }

    private Control StorageMutations(List<DnaMutationState> mutations, string key)
    {
        var selected = _storageSelection.GetValueOrDefault(key);
        if (selected >= mutations.Count)
            selected = 0;

        var tabs = VBox();
        for (var i = 0; i < mutations.Count; i++)
        {
            var index = i;
            tabs.AddChild(MakeButton(mutations[i].Name, () => { _storageSelection[key] = index; Rebuild(); }, false, null, i == selected));
        }

        var mutation = mutations.Count > 0 ? mutations[selected] : null;
        return HBox(
            new ScrollContainer { MinWidth = 140, MinHeight = 200, HScrollEnabled = false, Children = { tabs } },
            new Control { HorizontalExpand = true, Children = { Section(Loc.GetString("dna-console-mutation-info"), null, MutationInfo(mutation)) } });
    }

    private Control StorageChromosomes()
    {
        var available = ChromosomeKinds.Where(k => _state!.Chromosomes.GetValueOrDefault(k) > 0).ToList();
        var tabs = VBox();
        foreach (var kind in available)
        {
            tabs.AddChild(MakeButton(ChromoName(kind), () => { _selectedChromo = kind; Rebuild(); }, false, null, kind == _selectedChromo));
        }

        Control info;
        if (_selectedChromo is not { } chromo || !available.Contains(chromo))
        {
            info = Text(Loc.GetString("dna-console-nothing"), LabelColor);
        }
        else
        {
            info = VBox(
                LabeledText(Loc.GetString("dna-console-label-name"), ChromoName(chromo)),
                LabeledText(Loc.GetString("dna-console-label-description"), Loc.GetString($"dna-chromosome-{chromo.ToString().ToLowerInvariant()}-desc")),
                LabeledText(Loc.GetString("dna-console-label-amount"), _state!.Chromosomes.GetValueOrDefault(chromo).ToString()),
                MakeButton(Loc.GetString("dna-console-eject-chromosome"), () => Send(new DnaConsoleEjectChromoMessage(chromo))));
        }

        return HBox(
            new ScrollContainer { MinWidth = 140, MinHeight = 200, HScrollEnabled = false, Children = { tabs } },
            new Control { HorizontalExpand = true, Children = { Section(Loc.GetString("dna-console-chromosome-info"), null, info) } });
    }

    private Control AdvancedInjectors()
    {
        var state = _state!;
        var box = VBox();
        for (var i = 0; i < state.Injectors.Count; i++)
        {
            var injector = state.Injectors[i];
            var key = $"advinj{i}";
            var expanded = _expanded.Contains(key);
            var header = HBox(
                MakeButton((expanded ? "в–ј " : "в–є ") + injector.Name, () => Toggle(key)),
                new Control { HorizontalExpand = true },
                MakeButton(Loc.GetString("dna-console-print"), () => Send(new DnaConsolePrintAdvInjMessage(injector.Name)), !state.IsInjectorReady),
                MakeButton("вњ•", () => Send(new DnaConsoleDeleteAdvInjMessage(injector.Name)), false, Bad));
            header.HorizontalExpand = true;
            box.AddChild(header);
            if (expanded)
                box.AddChild(StorageMutations(injector.Mutations, key));
        }

        var name = new LineEdit { MinWidth = 200, Text = _newInjectorName, PlaceHolder = Loc.GetString("dna-console-advinj-name") };
        name.OnTextChanged += args => _newInjectorName = args.Text;
        box.AddChild(HBox(name, MakeButton(Loc.GetString("dna-console-advinj-create"), () =>
        {
            Send(new DnaConsoleNewAdvInjMessage(_newInjectorName));
            _newInjectorName = string.Empty;
        }, state.Injectors.Count >= state.MaxAdvInjectors)));

        return Section(Loc.GetString("dna-console-advinj-title"), null, box);
    }

    private void Toggle(string key)
    {
        if (!_expanded.Remove(key))
            _expanded.Add(key);

        Rebuild();
    }

    #endregion

    #region Р¤РµСЂРјРµРЅС‚С‹

    private void BuildEnzymes(string block, bool features)
    {
        var state = _state!;
        if (!state.IsScannerConnected)
        {
            _root.AddChild(Section(null, null, Text(Loc.GetString("dna-console-scanner-not-connected"), Bad)));
            return;
        }

        var strength = new SpinBox { Value = state.PulseStrength, MinWidth = 90 };
        strength.InitDefaultButtons();
        strength.IsValid = v => v is >= 1 and <= 15;
        strength.ValueChanged += args => Send(new DnaConsoleSetPulseStrengthMessage(args.Value));

        var duration = new SpinBox { Value = state.PulseDuration, MinWidth = 90 };
        duration.InitDefaultButtons();
        duration.IsValid = v => v is >= 1 and <= 30;
        duration.ValueChanged += args => Send(new DnaConsoleSetPulseDurationMessage(args.Value));

        var settings = Section(Loc.GetString("dna-console-emitter-title"), null, VBox(
            LabeledItem(Loc.GetString("dna-console-output-level"), strength),
            LabeledItem(Loc.GetString("dna-console-pulse-duration"), duration)));

        var probabilities = Section(Loc.GetString("dna-console-probabilities"), null, VBox(
            LabeledText(Loc.GetString("dna-console-accuracy"), state.StdDevAcc),
            LabeledText($"P(В±{state.StdDevStr})", "68 %"),
            LabeledText($"P(В±{state.StdDevStr * 2})", "95 %")));

        var blocks = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 1 };
        var column = VBox();
        for (var i = 0; i < block.Length; i++)
        {
            var index = i;
            column.AddChild(MakeButton(block[i].ToString(), () => Send(new DnaConsoleMakeupPulseMessage(features, index)), state.IsPulsing));
            if (column.ChildCount >= 3)
            {
                blocks.AddChild(column);
                column = VBox();
            }
        }

        if (column.ChildCount > 0)
            blocks.AddChild(column);

        var board = Section(Loc.GetString(features ? "dna-console-unique-features" : "dna-console-unique-enzymes"), null,
            block.Length == 0 ? Text(Loc.GetString("dna-console-nothing"), LabelColor) : blocks);

        _root.AddChild(HBox(
            new Control { MinWidth = 155, Children = { settings } },
            new Control { MinWidth = 140, Children = { probabilities } },
            new Control { HorizontalExpand = true, Children = { board } }));

        _root.AddChild(MakeupBuffers());
    }

    private Control MakeupBuffers()
    {
        var state = _state!;
        var box = VBox();
        if (state.GeneticMakeupCooldown > 0)
            box.AddChild(Centered(Loc.GetString("dna-console-makeup-cooldown", ("seconds", state.GeneticMakeupCooldown)), Average));

        for (var i = 0; i < state.MakeupStorage.Length; i++)
        {
            var index = i;
            var makeup = state.MakeupStorage[i];
            var key = $"makeup{i}";
            var expanded = _expanded.Contains(key);
            var title = makeup?.Name is { Length: > 0 } name ? name : Loc.GetString("dna-console-slot", ("index", i + 1));

            var header = HBox(MakeButton((expanded ? "в–ј " : "в–є ") + title, () => Toggle(key)), new Control { HorizontalExpand = true });
            if (state.HasDisk && state.DiskMakeup != null)
                header.AddChild(MakeButton(Loc.GetString("dna-console-import-disk"), () => Send(new DnaConsoleLoadMakeupDiskMessage(index))));
            header.AddChild(MakeButton(Loc.GetString("dna-console-save"), () => Send(new DnaConsoleSaveMakeupMessage(index)), !state.IsViableSubject));
            header.AddChild(MakeButton("вњ•", () => Send(new DnaConsoleDeleteMakeupMessage(index)), makeup == null, Bad));
            box.AddChild(header);

            if (expanded)
                box.AddChild(MakeupBufferInfo(index, makeup));
        }

        return Section(Loc.GetString("dna-console-makeup-buffers"), null, box);
    }

    private Control MakeupBufferInfo(int index, DnaMakeupState? makeup)
    {
        var state = _state!;
        if (makeup == null)
            return Text(Loc.GetString("dna-console-no-subject-data"), Average);

        var transfer = Loc.GetString("dna-console-transfer") + (state.IsViableSubject ? string.Empty : Loc.GetString("dna-console-delayed"));
        Control Row(string label, DnaMakeupType type) => LabeledItem(Loc.GetString(label), HBox(
            MakeButton(Loc.GetString("dna-console-print"), () => Send(new DnaConsoleMakeupInjectorMessage(index, type)), !state.IsInjectorReady),
            MakeButton(transfer, () => Send(new DnaConsoleApplyMakeupMessage(index, type)))));

        return VBox(
            GeneticMakeupInfo(makeup),
            Divider(),
            Text(Loc.GetString("dna-console-makeup-actions"), LabelColor),
            Row("dna-console-makeup-enzymes", DnaMakeupType.Enzymes),
            Row("dna-console-makeup-identity", DnaMakeupType.Identity),
            Row("dna-console-makeup-features", DnaMakeupType.Features),
            Row("dna-console-makeup-full", DnaMakeupType.Mixed),
            MakeButton(Loc.GetString("dna-console-export-disk"), () => Send(new DnaConsoleSaveMakeupDiskMessage(index)), !state.HasDisk || state.DiskReadOnly));
    }

    private static Control GeneticMakeupInfo(DnaMakeupState? makeup)
    {
        string Or(string? value) => string.IsNullOrEmpty(value) ? Loc.GetString("dna-console-none-value") : value;

        return Section(Loc.GetString("dna-console-enzyme-info"), null, VBox(
            LabeledText(Loc.GetString("dna-console-label-name"), Or(makeup?.Name)),
            LabeledText(Loc.GetString("dna-console-label-blood-type"), Or(makeup?.BloodType)),
            LabeledText(Loc.GetString("dna-console-label-ue"), Or(makeup?.UniqueEnzymes)),
            LabeledText(Loc.GetString("dna-console-label-ui"), Or(makeup?.UniqueIdentity)),
            LabeledText(Loc.GetString("dna-console-label-uf"), Or(makeup?.UniqueFeatures))));
    }

    #endregion
}
