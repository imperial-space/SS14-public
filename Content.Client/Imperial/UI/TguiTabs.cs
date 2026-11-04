using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.UI;

/// <summary>Tabs из tgui-core: вкладки с подсветкой выбранной и полосой снизу.</summary>
public sealed class TguiTabs : BoxContainer
{
    private readonly List<(TguiTab Tab, Action OnSelect)> _tabs = new();

    public TguiTabs()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 2;
    }

    public void AddTab(string text, string? icon, bool selected, Action onSelect)
    {
        var tab = new TguiTab(text, icon) { Selected = selected };
        tab.OnPressed += _ =>
        {
            foreach (var (other, _) in _tabs)
                other.Selected = other == tab;
            onSelect();
        };
        _tabs.Add((tab, onSelect));
        AddChild(tab);
    }

    private sealed class TguiTab : BaseButton
    {
        private bool _selected;

        public TguiTab(string text, string? icon)
        {
            MouseFilter = MouseFilterMode.Stop;
            var box = Tgui.HBox(6);
            box.Margin = new Thickness(10, 4, 10, 6);
            if (icon != null)
                box.AddChild(TguiIcons.Icon(icon, 12, Tgui.TextColor));
            box.AddChild(Tgui.Text(text, Tgui.TextColor));
            AddChild(box);
        }

        public bool Selected
        {
            get => _selected;
            set => _selected = value;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var rect = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
            if (_selected)
            {
                handle.DrawRect(rect, Color.White.WithAlpha(0.075f));
                handle.DrawRect(new UIBox2(0, PixelSize.Y - 2 * UIScale, PixelSize.X, PixelSize.Y), Color.FromHex("#d4dfec"));
            }
            else if (DrawMode == DrawModeEnum.Hover)
            {
                handle.DrawRect(rect, Color.White.WithAlpha(0.04f));
            }

            foreach (var child in Children)
            {
                foreach (var label in child.Children)
                {
                    if (label is Label l)
                        l.FontColorOverride = _selected || DrawMode == DrawModeEnum.Hover ? Tgui.TextColor : Color.White.WithAlpha(0.5f);
                }
            }
        }
    }
}
