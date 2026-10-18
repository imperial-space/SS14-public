using System.Numerics;
using Content.Client.Message;
using Content.Shared.Imperial.Changeling;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Changeling;

/// <summary>
/// tgui_alert (до трёх вариантов — кнопки в ряд) и tgui_input_list (список с поиском) для способностей генокрада.
/// </summary>
[UsedImplicitly]
public sealed class ImperialChangelingChoiceBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.SetSize = new Vector2(350, 150);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not ImperialChangelingChoiceState choice)
            return;

        _window.Title = choice.Title;
        _window.Contents.RemoveAllChildren();

        var background = new PanelContainer
        {
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = TguiStyle.WindowBackground },
            VerticalExpand = true,
        };
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 8 };
        background.AddChild(column);
        _window.Contents.AddChild(background);

        var message = new RichTextLabel { HorizontalAlignment = Control.HAlignment.Center };
        message.SetMessage(choice.Message);
        column.AddChild(message);

        if (choice.Options.Count <= 3)
        {
            BuildAlert(column, choice);
            return;
        }

        BuildList(column, choice);
    }

    /// <summary>tgui_alert: кнопки в ряд.</summary>
    private void BuildAlert(BoxContainer column, ImperialChangelingChoiceState choice)
    {
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalAlignment = Control.HAlignment.Center, SeparationOverride = 8 };
        for (var i = 0; i < choice.Options.Count; i++)
        {
            var index = i;
            var button = new Button { Text = choice.Options[i], MinWidth = 80 };
            TguiStyle.Paint(button, TguiStyle.Default);
            button.OnPressed += _ => SendMessage(new ImperialChangelingChoiceMessage(index));
            buttons.AddChild(button);
        }

        column.AddChild(buttons);
    }

    /// <summary>tgui_input_list: поиск, список и «Да» / «Отмена».</summary>
    private void BuildList(BoxContainer column, ImperialChangelingChoiceState choice)
    {
        _window!.SetSize = new Vector2(325, 355);
        var search = new LineEdit { PlaceHolder = Loc.GetString("imperial-changeling-emporium-search") };
        column.AddChild(search);

        var list = new ItemList { VerticalExpand = true, SelectMode = ItemList.ItemListSelectMode.Single };
        column.AddChild(list);

        void Fill()
        {
            list.Clear();
            for (var i = 0; i < choice.Options.Count; i++)
            {
                if (search.Text.Length > 0 && !choice.Options[i].Contains(search.Text, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                list.AddItem(choice.Options[i]).Metadata = i;
            }
        }

        Fill();
        search.OnTextChanged += _ => Fill();

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalAlignment = Control.HAlignment.Center, SeparationOverride = 8 };
        var ok = new Button { Text = Loc.GetString("imperial-changeling-choice-yes"), MinWidth = 80 };
        TguiStyle.Paint(ok, TguiStyle.Good);
        ok.OnPressed += _ =>
        {
            foreach (var item in list.GetSelected())
            {
                if (item.Metadata is int index)
                    SendMessage(new ImperialChangelingChoiceMessage(index));
                return;
            }
        };
        var cancel = new Button { Text = Loc.GetString("imperial-changeling-choice-cancel"), MinWidth = 80 };
        TguiStyle.Paint(cancel, TguiStyle.Bad);
        cancel.OnPressed += _ => SendMessage(new ImperialChangelingChoiceMessage(-1));
        buttons.AddChild(ok);
        buttons.AddChild(cancel);
        column.AddChild(buttons);
    }
}
