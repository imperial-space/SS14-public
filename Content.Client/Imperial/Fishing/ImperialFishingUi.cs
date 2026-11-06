using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Fishing;

/// <summary>Общие элементы окон рыбалки, повторяющие компоненты tgui (Section, LabeledList, DmIcon, Dimmer).</summary>
public static class ImperialFishingUi
{
    public static readonly ResPath HeartsRsi = new("/Textures/Imperial/Fishing/hearts.rsi");

    /// <summary>Section: заголовок с полосой и содержимое.</summary>
    public static PanelContainer Section(string? title, Control content, Color? background = null)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = background ?? Color.FromHex("#131317").WithAlpha(0.75f),
                BorderColor = Color.FromHex("#4972a1").WithAlpha(0.4f),
                BorderThickness = new Thickness(0, 0, 0, 0),
            },
            Margin = new Thickness(2),
        };

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        if (title != null)
        {
            box.AddChild(new Label
            {
                Text = title,
                FontColorOverride = Color.White,
                Margin = new Thickness(6, 4, 6, 2),
                StyleClasses = { "LabelHeading" },
            });
            box.AddChild(new PanelContainer
            {
                MinHeight = 2,
                PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#4972a1") },
                Margin = new Thickness(4, 0, 4, 4),
            });
        }

        content.Margin = new Thickness(6, 2, 6, 6);
        box.AddChild(content);
        panel.AddChild(box);
        return panel;
    }

    /// <summary>Карточка с рамкой (рыбы в аквариуме и анализаторе).</summary>
    public static PanelContainer Card(Color background, Color border, int borderWidth = 3)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = background,
                BorderColor = border,
                BorderThickness = new Thickness(borderWidth),
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5,
            },
            Margin = new Thickness(4),
        };
    }

    /// <summary>DmIcon: иконка прототипа заданного размера на фоне.</summary>
    public static Control Icon(SpriteSystem sprite, string? proto, float size, Color? background = null)
    {
        var rect = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MinSize = new Vector2(size, size),
            SetSize = new Vector2(size, size),
        };

        if (proto != null)
            rect.Texture = sprite.GetPrototypeIcon(proto).Default;

        if (background == null)
            return rect;

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = background.Value },
            MinSize = new Vector2(size, size),
            SetSize = new Vector2(size, size),
        };
        panel.AddChild(rect);
        return panel;
    }

    public static TextureRect Texture(SpriteSystem sprite, SpriteSpecifier specifier, float size)
    {
        return new TextureRect
        {
            Texture = sprite.Frame0(specifier),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            MinSize = new Vector2(size, size),
            SetSize = new Vector2(size, size),
        };
    }

    /// <summary>LabeledList.Item: метка справа выровнена, значение переносится.</summary>
    public static BoxContainer LabeledItem(string label, Control value, float labelWidth = 150)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 2) };
        row.AddChild(new Label
        {
            Text = label + ":",
            MinWidth = labelWidth,
            SetWidth = labelWidth,
            FontColorOverride = Color.FromHex("#9ab4cf"),
            VerticalAlignment = Control.VAlignment.Top,
            Margin = new Thickness(0, 0, 6, 0),
        });
        value.HorizontalExpand = true;
        row.AddChild(value);
        return row;
    }

    public static RichTextLabel Text(string text, Color? color = null)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        var msg = new FormattedMessage();
        if (color != null)
            msg.PushColor(color.Value);
        msg.AddText(text);
        label.SetMessage(msg);
        return label;
    }

    /// <summary>Dimmer «ЗАБЛОКИРОВАНО» поверх секции в автоматическом режиме.</summary>
    public static Control Locked(SpriteSystem sprite)
    {
        var dimmer = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black.WithAlpha(0.75f) },
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        };
        var icon = Texture(sprite, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/lock.svg.192dpi.png")), 36);
        icon.Modulate = Color.Red;
        icon.HorizontalAlignment = Control.HAlignment.Center;
        box.AddChild(icon);
        box.AddChild(new Label
        {
            Text = Loc.GetString("imperial-aquarium-ui-locked"),
            FontColorOverride = Color.White,
            HorizontalAlignment = Control.HAlignment.Center,
            StyleClasses = { "LabelHeading" },
        });
        dimmer.AddChild(box);
        return dimmer;
    }
}
