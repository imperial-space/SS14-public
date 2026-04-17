using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно выбора предмета для создания культовой структурой (алтарь/кузница/архив).
/// </summary>
public sealed class CultStructureWindow : DefaultWindow
{
    public event Action<string>? OnItemSelected;

    private readonly BoxContainer _vbox;

    public CultStructureWindow()
    {
        Title = Loc.GetString("cult-structure-window-title");
        MinSize = new Vector2(260, 200);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        _vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
        };

        scroll.AddChild(_vbox);
        Contents.AddChild(scroll);
    }

    /// <summary>
    /// Обновляет список доступных предметов.
    /// </summary>
    public void SetItems(List<(string ItemId, string LocKey)> items)
    {
        _vbox.RemoveAllChildren();

        if (items.Count == 0)
        {
            _vbox.AddChild(new Label
            {
                Text = Loc.GetString("cult-structure-no-items"),
                HorizontalAlignment = HAlignment.Center,
            });
            return;
        }

        foreach (var (itemId, locKey) in items)
        {
            var capturedId = itemId;
            var btn = new Button
            {
                Text = Loc.GetString(locKey),
                HorizontalExpand = true,
                MinSize = new Vector2(0, 36),
            };
            btn.OnPressed += _ =>
            {
                OnItemSelected?.Invoke(capturedId);
                Close();
            };
            _vbox.AddChild(btn);
        }
    }
}
