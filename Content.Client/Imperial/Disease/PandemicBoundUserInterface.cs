using System.Numerics;
using Content.Shared.Imperial.Disease;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Disease;

public sealed class PandemicBoundUserInterface : BoundUserInterface
{
    private PandemicWindow? _window;

    public PandemicBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PandemicWindow>();
        _window.EjectBeaker += () => SendMessage(new PandemicEjectBeakerMessage());
        _window.EmptyBeaker += () => SendMessage(new PandemicEmptyBeakerMessage());
        _window.CreateCulture += index => SendMessage(new PandemicCreateCultureMessage(index));
        _window.CreateVaccine += id => SendMessage(new PandemicCreateVaccineMessage(id));
        _window.RenameDisease += (index, name) => SendMessage(new PandemicRenameDiseaseMessage(index, name));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PandemicBoundUserInterfaceState pandemicState)
            _window?.UpdateState(pandemicState);
    }
}

/// <summary>Окно PanD.E.M.I.C 2200: анализ образца, печать культуры и вакцины.</summary>
public sealed class PandemicWindow : DefaultWindow
{
    public event Action? EjectBeaker;
    public event Action? EmptyBeaker;
    public event Action<int>? CreateCulture;
    public event Action<string>? CreateVaccine;
    public event Action<int, string>? RenameDisease;

    private readonly BoxContainer _body = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 6,
    };

    public PandemicWindow()
    {
        Title = Loc.GetString("pandemic-title");
        MinSize = SetSize = new Vector2(520, 560);
        Contents.AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            Children = { _body },
        });
    }

    public void UpdateState(PandemicBoundUserInterfaceState state)
    {
        _body.RemoveAllChildren();

        if (!state.HasBeaker)
        {
            _body.AddChild(new Label { Text = Loc.GetString("pandemic-no-beaker") });
            return;
        }

        // Строка с ёмкостью и кнопками.
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        header.AddChild(new Label
        {
            Text = Loc.GetString("pandemic-beaker", ("volume", state.Volume), ("capacity", state.Capacity)),
            HorizontalExpand = true,
        });

        var empty = new Button { Text = Loc.GetString("pandemic-empty") };
        empty.OnPressed += _ => EmptyBeaker?.Invoke();
        header.AddChild(empty);

        var eject = new Button { Text = Loc.GetString("pandemic-eject") };
        eject.OnPressed += _ => EjectBeaker?.Invoke();
        header.AddChild(eject);
        _body.AddChild(header);

        if (!state.HasBlood)
        {
            _body.AddChild(new Label { Text = Loc.GetString("pandemic-no-blood") });
            return;
        }

        _body.AddChild(new Label { Text = Loc.GetString("pandemic-dna", ("dna", state.Dna)) });

        foreach (var disease in state.Diseases)
        {
            _body.AddChild(BuildDisease(disease, state.Ready));
        }

        if (state.Resistances.Length > 0)
        {
            _body.AddChild(new Label { Text = Loc.GetString("pandemic-resistances"), StyleClasses = { "LabelHeading" } });
            foreach (var resistance in state.Resistances)
            {
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
                row.AddChild(new Label { Text = resistance.Name, HorizontalExpand = true });

                var vaccine = new Button { Text = Loc.GetString("pandemic-create-vaccine"), Disabled = !state.Ready };
                var id = resistance.Id;
                vaccine.OnPressed += _ => CreateVaccine?.Invoke(id);
                row.AddChild(vaccine);
                _body.AddChild(row);
            }
        }
    }

    private Control BuildDisease(PandemicDiseaseData disease, bool ready)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };

        var title = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        title.AddChild(new Label { Text = disease.Name, StyleClasses = { "LabelHeading" }, HorizontalExpand = true });

        if (disease.Advanced)
        {
            var culture = new Button { Text = Loc.GetString("pandemic-create-culture"), Disabled = !ready };
            var index = disease.Index;
            culture.OnPressed += _ => CreateCulture?.Invoke(index);
            title.AddChild(culture);
        }

        box.AddChild(title);

        box.AddChild(new Label { Text = Loc.GetString("pandemic-agent", ("agent", disease.Agent)) });
        box.AddChild(new Label { Text = Loc.GetString("pandemic-spread", ("spread", disease.Spread)) });
        box.AddChild(new Label { Text = Loc.GetString("pandemic-cure", ("cure", disease.CureText)) });

        if (disease.Advanced)
        {
            box.AddChild(new Label
            {
                Text = Loc.GetString("pandemic-stats",
                    ("resistance", disease.Resistance),
                    ("stageSpeed", disease.StageSpeed),
                    ("stealth", disease.Stealth),
                    ("transmission", disease.Transmission),
                    ("severity", disease.Severity)),
            });

            if (disease.CanRename)
            {
                var renameRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
                var field = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("pandemic-rename-placeholder") };
                renameRow.AddChild(field);

                var rename = new Button { Text = Loc.GetString("pandemic-rename") };
                var index = disease.Index;
                rename.OnPressed += _ => RenameDisease?.Invoke(index, field.Text);
                renameRow.AddChild(rename);
                box.AddChild(renameRow);
            }

            foreach (var symptom in disease.Symptoms)
            {
                box.AddChild(BuildSymptom(symptom));
            }
        }

        return new PanelContainer
        {
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.25f) },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Margin = new Thickness(8, 6),
                    Children = { box },
                },
            },
        };
    }

    private Control BuildSymptom(PandemicSymptomData symptom)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Margin = new Thickness(12, 4, 0, 0),
        };

        box.AddChild(new Label { Text = symptom.Name, StyleClasses = { "LabelSubText" } });
        box.AddChild(new RichTextLabel { Text = symptom.Description });
        box.AddChild(new Label
        {
            Text = Loc.GetString("pandemic-symptom-stats",
                ("stealth", symptom.Stealth),
                ("resistance", symptom.Resistance),
                ("stageSpeed", symptom.StageSpeed),
                ("transmission", symptom.Transmission),
                ("level", symptom.Level)),
        });
        box.AddChild(new Label { Text = Loc.GetString("pandemic-symptom-cure", ("cure", symptom.Cure)) });

        foreach (var threshold in symptom.Thresholds)
        {
            box.AddChild(new RichTextLabel { Text = threshold });
        }

        return box;
    }
}
