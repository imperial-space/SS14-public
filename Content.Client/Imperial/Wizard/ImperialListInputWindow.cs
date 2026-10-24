using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Wizard;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Wizard;

/// <summary>Открывает окно выбора из списка по запросу сервера.</summary>
public sealed class ImperialListInputSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialWizardListInputEvent>(OnInput);
    }

    private void OnInput(ImperialWizardListInputEvent ev)
    {
        var answered = false;
        var window = new ImperialListInputWindow(ev.Title, ev.Message, ev.Options);
        window.OnChoice += choice =>
        {
            answered = true;
            RaiseNetworkEvent(new ImperialWizardListChoiceEvent(ev.Id, choice));
            window.Close();
        };
        window.OnClose += () =>
        {
            if (!answered)
                RaiseNetworkEvent(new ImperialWizardListChoiceEvent(ev.Id, null));
        };
        window.OpenCentered();
    }
}

/// <summary>ListInputModal tgui: сообщение, поиск, список, «Выбрать» / «Отмена».</summary>
public sealed class ImperialListInputWindow : DefaultWindow
{
    public event Action<string>? OnChoice;

    private readonly List<string> _options;
    private readonly BoxContainer _list = Tgui.VBox(2);
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private string? _selected;

    public ImperialListInputWindow(string title, string message, List<string> options)
    {
        Title = title;
        _options = options;
        MinSize = SetSize = new Vector2(325, 420);

        _search.PlaceHolder = Loc.GetString("imperial-list-input-search");
        _search.OnTextChanged += _ => Rebuild();
        _search.OnTextEntered += _ => Submit();

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_list);

        var buttons = Tgui.HBox(6,
            Tgui.Button(Loc.GetString("imperial-list-input-submit"), Submit, fluid: true),
            Tgui.Button(Loc.GetString("imperial-list-input-cancel"), Close, fluid: true));

        var content = Tgui.VBox(6,
            Tgui.Paragraph(message),
            Tgui.MakeSection(null, null, scroll),
            _search,
            buttons);
        content.Margin = new Thickness(6);
        scroll.Parent!.VerticalExpand = true;
        scroll.Parent!.Parent!.VerticalExpand = true;
        Contents.AddChild(Tgui.Window(content, scrollable: false));
        Rebuild();
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        var search = _search.Text.Trim();
        foreach (var option in _options)
        {
            if (search.Length > 0 && !option.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            var current = option;
            var button = Tgui.Button(option, () =>
            {
                _selected = current;
                Rebuild();
            }, option == _selected ? Tgui.Green : Tgui.Transparent, fluid: true);
            button.TextLabel.HorizontalAlignment = HAlignment.Left;
            _list.AddChild(button);
        }
    }

    private void Submit()
    {
        var choice = _selected;
        if (choice == null)
        {
            var search = _search.Text.Trim();
            choice = _options.Find(o => search.Length > 0 && o.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (choice != null)
            OnChoice?.Invoke(choice);
    }
}
