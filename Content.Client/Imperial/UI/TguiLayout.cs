using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.UI;

/// <summary>Составные элементы tgui: LabeledControls, Collapsible, Tabs, Button.Checkbox.</summary>
public static class TguiLayout
{
    /// <summary>LabeledControls.Item: элемент по центру, подпись цвета label под ним.</summary>
    public static Control LabeledControl(string label, Control control)
    {
        control.HorizontalAlignment = Control.HAlignment.Center;
        var text = Tgui.Text(label, Tgui.Label, align: Control.HAlignment.Center);
        var box = Tgui.VBox(4, control, text);
        box.Margin = new Thickness(6, 2);
        return box;
    }

    /// <summary>Collapsible: кнопка на всю ширину со стрелкой, по клику показывает содержимое.</summary>
    public static Control Collapsible(string title, Control content, bool open = false)
    {
        var button = new TguiButton(title, Tgui.Primary) { HorizontalExpand = true };
        button.TextLabel.HorizontalAlignment = Control.HAlignment.Left;
        content.Visible = open;
        button.SetIcon(open ? "chevron-down" : "chevron-right");
        button.OnPressed += _ =>
        {
            content.Visible = !content.Visible;
            button.SetIcon(content.Visible ? "chevron-down" : "chevron-right");
        };
        return Tgui.VBox(4, button, content);
    }

    /// <summary>Button.Checkbox: прозрачная кнопка с квадратиком.</summary>
    public static TguiButton Checkbox(string text, bool @checked, Action onPressed)
    {
        var button = new TguiButton(text, @checked ? Tgui.Green : Tgui.Transparent);
        button.SetIcon(@checked ? "check-square-o" : "square-o");
        button.OnPressed += _ => onPressed();
        return button;
    }
}
