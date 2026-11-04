using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Cult;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// show_radial_menu / tgui_input_list / tgui_alert культа.
/// Закрытием управляет сервер: после выбора он может сразу открыть следующее меню того же ключа
/// (подтверждение руны Нар'Си, удаление заклинания), поэтому клиент сам BUI не закрывает.
/// </summary>
[UsedImplicitly]
public sealed class CultChoiceBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _radial;
    private DefaultWindow? _window;
    private bool _replacing;
    private bool _answered;

    public CultChoiceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not CultChoiceState choice)
            return;

        CloseWindows();
        _answered = false;

        if (choice.Radial && choice.Options.All(o => o.Icon != null))
        {
            _radial = new SimpleRadialMenu();
            _radial.OnClose += OnWindowClosed;
            _radial.Track(Owner);
            var buttons = choice.Options.Select(o => (RadialMenuOptionBase) new RadialMenuActionOption<string>(Pick, o.Id)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(o.Icon!),
                ToolTip = o.Tooltip == null ? o.Label : $"{o.Label}\n{o.Tooltip}",
            }).ToList();
            _radial.SetButtons(buttons);
            _radial.OpenOverMouseScreenPosition();
            return;
        }

        _window = new DefaultWindow
        {
            Title = choice.Title,
            MinSize = new Vector2(280, 120),
        };
        _window.OnClose += OnWindowClosed;

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
        };
        foreach (var option in choice.Options)
        {
            var button = new Button
            {
                Text = option.Label,
                ToolTip = option.Tooltip,
                HorizontalExpand = true,
                ClipText = true,
            };
            if (option.Color is { } color)
                button.ModulateSelfOverride = color;
            var id = option.Id;
            button.OnPressed += _ => Pick(id);
            box.AddChild(button);
        }

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, MaxHeight = 500 };
        scroll.AddChild(box);
        _window.Contents.AddChild(scroll);
        _window.OpenCentered();
    }

    private void Pick(string id)
    {
        if (_answered)
            return;
        _answered = true;
        SendMessage(new CultChoiceMessage(id));
        // Окно прячем, но BUI не закрываем: сервер закроет или пришлёт следующее меню.
        CloseWindows();
    }

    /// <summary>Игрок сам закрыл окно (крестик/клик мимо радиалки) — отказ от выбора.</summary>
    private void OnWindowClosed()
    {
        if (_replacing || _answered)
            return;
        Close();
    }

    private void CloseWindows()
    {
        _replacing = true;
        _radial?.Close();
        _window?.Close();
        _radial = null;
        _window = null;
        _replacing = false;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            CloseWindows();
    }
}
