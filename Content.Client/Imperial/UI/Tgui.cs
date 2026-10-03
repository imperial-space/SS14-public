using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.UI;

/// <summary>Тема tgui: цвета фона, секций, primary, подписей и плашек (tgui-core/styles/themes).</summary>
public sealed class TguiTheme
{
    public Color BaseStart = Color.FromHex("#2a2a2a");
    public Color BaseEnd = Color.FromHex("#202020");
    public Color Section = Color.FromHex("#191919");
    public Color Primary = Color.FromHex("#40628a");
    public Color SectionLine = Color.FromHex("#4972a1");
    public Color Label = Color.FromHex("#7e90a7");
    public Color Notice = Color.FromHex("#bb9b68");
    public Color NoticeText = Color.Black;
    public Color Selected = Color.FromHex("#1fa952");
    public Color Danger = Color.FromHex("#d92626");
    public Color Header = Color.FromHex("#1b1b1b");

    /// <summary>Тема по умолчанию.</summary>
    public static readonly TguiTheme Default = new();

    /// <summary>
    /// theme-syndicate: --color-base hsl(0, 96%, 17%) с градиентом 6, --color-primary hsl(120, 34%, 35%),
    /// плашки hsl(0, 98%, 28%), выбранные кнопки hsl(0, 90%, 37.5%).
    /// </summary>
    public static readonly TguiTheme Syndicate = new()
    {
        BaseStart = Color.FromHex("#6f0404"),
        BaseEnd = Color.FromHex("#3b0202"),
        Section = Color.FromHex("#360101"),
        Primary = Color.FromHex("#3b773b"),
        SectionLine = Color.FromHex("#3b773b"),
        Label = Color.FromHex("#80aa80"),
        Notice = Color.FromHex("#750101"),
        NoticeText = Color.White,
        Selected = Color.FromHex("#b50909"),
        Danger = Color.FromHex("#999900"),
        Header = Color.FromHex("#260000"),
    };
}

/// <summary>
/// Контролы в стиле tgui-core (тема по умолчанию SS13): Section, LabeledList, ProgressBar,
/// Button, NoticeBox. Цвета взяты из tgui-core/styles/vars-colors.scss.
/// </summary>
public static class Tgui
{
    // --color-base: hsl(0, 0%, 15%) с градиентом; --color-section: hsla(0, 0%, 0%, 0.33).
    public static readonly Color Base = Color.FromHex("#252525");
    public static readonly Color Section = Color.FromHex("#191919");
    public static readonly Color Primary = Color.FromHex("#40628a");
    public static readonly Color PrimaryLine = Color.FromHex("#4972a1");
    public static readonly Color Label = Color.FromHex("#7e90a7");
    public static readonly Color TextColor = Color.FromHex("#ffffff");

    public static readonly Color Good = Color.FromHex("#5baa27");
    public static readonly Color Average = Color.FromHex("#f29a1d");
    public static readonly Color Bad = Color.FromHex("#df3e3e");

    public static readonly Color Red = Color.FromHex("#d92626");
    public static readonly Color Orange = Color.FromHex("#f26d0d");
    public static readonly Color Yellow = Color.FromHex("#fbd608");
    public static readonly Color Green = Color.FromHex("#1fa952");
    public static readonly Color Blue = Color.FromHex("#2a7bc9");
    public static readonly Color Grey = Color.FromHex("#7f7f7f");
    public static readonly Color Brown = Color.FromHex("#a5682a");

    // --notice-box-background: hsl(37.5, 55%, 57.5%), затемнённый в 0.825 раза.
    public static readonly Color Notice = Color.FromHex("#bb9b68");

    public static readonly Color Disabled = Color.FromHex("#757575");

    /// <summary>color="transparent": кнопка без заливки, подсвечивается при наведении.</summary>
    public static readonly Color Transparent = Color.Transparent;

    private static readonly Dictionary<(int, bool, bool), Font> FontCache = new();

    /// <summary>
    /// Стек шрифтов как у SS14: NotoSans, затем символы и эмодзи — иначе значки вроде ⌖ ☠ ► рисуются квадратами.
    /// </summary>
    public static Font Font(int size, bool bold = false, bool italic = false)
    {
        if (FontCache.TryGetValue((size, bold, italic), out var cached))
            return cached;

        var cache = IoCManager.Resolve<IResourceCache>();
        var main = (bold, italic) switch
        {
            (true, true) => "/Fonts/NotoSans/NotoSans-BoldItalic.ttf",
            (true, false) => "/Fonts/NotoSans/NotoSans-Bold.ttf",
            (false, true) => "/Fonts/NotoSans/NotoSans-Italic.ttf",
            _ => "/Fonts/NotoSans/NotoSans-Regular.ttf",
        };
        string[] paths =
        {
            main,
            bold ? "/Fonts/NotoSans/NotoSansSymbols-Bold.ttf" : "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
            "/Fonts/NotoEmoji.ttf",
        };

        var fonts = new Font[paths.Length];
        for (var i = 0; i < paths.Length; i++)
        {
            fonts[i] = new VectorFont(cache.GetResource<FontResource>(paths[i]), size);
        }

        var font = new StackedFont(fonts);
        FontCache[(size, bold, italic)] = font;
        return font;
    }

    public static Font MonoFont(int size)
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        return new VectorFont(cache.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Regular.ttf"), size);
    }

    public static Color Lighten(Color color, float amount)
    {
        return new Color(
            MathF.Min(1, color.R + amount),
            MathF.Min(1, color.G + amount),
            MathF.Min(1, color.B + amount),
            color.A);
    }

    /// <summary>Фон окна tgui: Window.Content с вертикальным градиентом темы.</summary>
    public static Control Window(Control content, TguiTheme? theme = null, bool scrollable = true)
    {
        theme ??= TguiTheme.Default;
        var root = new GradientPanel(theme.BaseStart, theme.BaseEnd)
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        if (scrollable)
        {
            root.AddChild(new ScrollContainer
            {
                HScrollEnabled = false,
                VerticalExpand = true,
                Children = { content },
            });
        }
        else
        {
            root.AddChild(content);
        }

        return root;
    }

    public static BoxContainer VBox(int separation = 6, params Control[] children)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
        foreach (var child in children)
        {
            box.AddChild(child);
        }

        return box;
    }

    public static BoxContainer HBox(int separation = 4, params Control[] children)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
        foreach (var child in children)
        {
            box.AddChild(child);
        }

        return box;
    }

    public static Label Text(string text, Color? color = null, int size = 12, bool bold = false,
        Control.HAlignment align = Control.HAlignment.Left, bool italic = false)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = color ?? TextColor,
            FontOverride = Font(size, bold, italic),
            HorizontalAlignment = align,
        };
    }

    /// <summary>Многострочный текст с переносом (Box с обычным текстом).</summary>
    public static RichTextLabel Paragraph(string markup, Color? color = null)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        var message = new Robust.Shared.Utility.FormattedMessage();
        if (color is { } c)
            message.PushColor(c);
        message.AddMarkupPermissive(markup);
        if (color != null)
            message.Pop();
        label.SetMessage(message);
        return label;
    }

    /// <summary>
    /// Section: плашка, заголовок 14px жирным, под ним полоса цвета primary,
    /// справа от заголовка — кнопки.
    /// </summary>
    public static Control MakeSection(string? title, Control? buttons, Control content, TguiTheme? theme = null)
    {
        theme ??= TguiTheme.Default;
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        if (title != null || buttons != null)
        {
            var header = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Margin = new Thickness(6, 5, 6, 5),
            };
            header.AddChild(new Label
            {
                Text = title ?? string.Empty,
                FontOverride = Font(14, true),
                FontColorOverride = TextColor,
                HorizontalExpand = true,
                VerticalAlignment = Control.VAlignment.Center,
                ClipText = true,
            });
            if (buttons != null)
                header.AddChild(buttons);

            box.AddChild(header);
            box.AddChild(new PanelContainer
            {
                MinHeight = 2,
                PanelOverride = new StyleBoxFlat { BackgroundColor = theme.SectionLine },
            });
        }

        content.Margin = new Thickness(6, 8, 6, 8);
        box.AddChild(content);
        // Секция с fill (Section fill в tgui): растягивается вместе с содержимым.
        if (content.VerticalExpand)
            box.VerticalExpand = true;

        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = theme.Section },
            Children = { box },
        };
    }

    /// <summary>LabeledList: подписи цвета label в отдельной колонке.</summary>
    public static GridContainer LabeledList(params (string Label, Control Value)[] items)
    {
        return LabeledList(null, items);
    }

    public static GridContainer LabeledList(TguiTheme? theme, params (string Label, Control Value)[] items)
    {
        var grid = new GridContainer { Columns = 2, HSeparationOverride = 8, VSeparationOverride = 4 };
        foreach (var (label, value) in items)
        {
            AddItem(grid, label, value, theme);
        }

        return grid;
    }

    public static void AddItem(GridContainer list, string label, Control value, TguiTheme? theme = null)
    {
        theme ??= TguiTheme.Default;
        list.AddChild(new Label
        {
            Text = label + ":",
            FontColorOverride = theme.Label,
            FontOverride = Font(12),
            VerticalAlignment = Control.VAlignment.Center,
        });
        value.HorizontalExpand = true;
        list.AddChild(value);
    }

    /// <summary>ProgressBar: рамка и заливка цвета значения, текст поверх.</summary>
    public static Control ProgressBar(float ratio, Color color, string text)
    {
        ratio = Math.Clamp(ratio, 0f, 1f);
        var fill = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = color },
            HorizontalAlignment = Control.HAlignment.Left,
        };

        var root = new ProgressFill(fill, ratio)
        {
            MinHeight = 22,
            HorizontalExpand = true,
        };

        root.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderColor = color,
                BorderThickness = new Thickness(1),
            },
        });
        root.AddChild(fill);
        root.AddChild(new Label
        {
            Text = text,
            FontOverride = Font(12),
            FontColorOverride = TextColor,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        });
        return root;
    }

    /// <summary>Цвет по долям: good ≥ 1, average ≥ 0.3, иначе bad — как ranges в tgui.</summary>
    public static Color RangeColor(float ratio)
    {
        return ratio >= 1 ? Good : ratio >= 0.3f ? Average : Bad;
    }

    /// <summary>NoticeBox: плашка темы (или цветная) с жирным курсивом.</summary>
    public static Control NoticeBox(string text, Color? color = null, TguiTheme? theme = null)
    {
        theme ??= TguiTheme.Default;
        var background = color ?? theme.Notice;
        var textColor = color == null ? theme.NoticeText : color == Yellow ? Color.Black : TextColor;
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = background },
            Children =
            {
                new Label
                {
                    Text = text,
                    FontColorOverride = textColor,
                    FontOverride = Font(12, true, true),
                    Margin = new Thickness(8, 5),
                },
            },
        };
    }

    public static TguiButton Button(string text, Action? onPressed = null, Color? color = null, bool disabled = false,
        bool fluid = false, int fontSize = 12, bool bold = false, string? tooltip = null, TguiTheme? theme = null, string? icon = null)
    {
        theme ??= TguiTheme.Default;
        var button = new TguiButton(text, color ?? theme.Primary, fontSize, bold)
        {
            Disabled = disabled,
            HorizontalExpand = fluid,
            ToolTip = tooltip,
        };
        button.SetIcon(icon);

        if (onPressed != null)
            button.OnPressed += _ => onPressed();

        return button;
    }

    /// <summary>Размер-подложка для заливки ProgressBar.</summary>
    private sealed class ProgressFill(PanelContainer fill, float ratio) : Control
    {
        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            foreach (var child in Children)
            {
                if (child == fill)
                    child.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(finalSize.X * ratio, finalSize.Y)));
                else
                    child.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
            }

            return finalSize;
        }
    }

    /// <summary>Фон с вертикальным градиентом (Layout__content в tgui).</summary>
    private sealed class GradientPanel(Color top, Color bottom) : Control
    {
        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            const int bands = 48;
            var size = PixelSize;
            for (var i = 0; i < bands; i++)
            {
                var t = i / (float) (bands - 1);
                var color = Color.InterpolateBetween(top, bottom, t);
                var y0 = size.Y * i / (float) bands;
                var y1 = size.Y * (i + 1) / (float) bands;
                handle.DrawRect(new UIBox2(0, y0, size.X, y1 + 1), color);
            }
        }
    }
}

/// <summary>Плоская кнопка tgui: заливка цветом, светлее при наведении, серая при отключении.</summary>
public sealed class TguiButton : BaseButton
{
    private readonly PanelContainer _panel;
    private readonly StyleBoxFlat _style;
    private Color _color;

    public readonly Label TextLabel;
    private readonly Label _icon;
    private readonly int _fontSize;

    public TguiButton(string text, Color color, int fontSize = 12, bool bold = false)
    {
        _fontSize = fontSize;
        _color = color;
        _style = new StyleBoxFlat { BackgroundColor = color, ContentMarginLeftOverride = 6, ContentMarginRightOverride = 6 };
        TextLabel = new Label
        {
            Text = text,
            FontOverride = Tgui.Font(fontSize, bold),
            FontColorOverride = Tgui.TextColor,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(4, 2),
        };
        _icon = new Label { Visible = false, VerticalAlignment = VAlignment.Center, Margin = new Thickness(4, 2, 0, 2) };
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Children = { _icon, TextLabel } };
        TextLabel.HorizontalExpand = true;
        _panel = new PanelContainer { PanelOverride = _style, Children = { content } };
        AddChild(_panel);
        MinHeight = 22;
        MouseFilter = MouseFilterMode.Stop;
        DrawModeChanged();
    }

    /// <summary>Иконка Font Awesome слева от текста, как у Button icon=... в tgui.</summary>
    public void SetIcon(string? icon)
    {
        if (icon == null || !TguiIcons.TryGet(icon, out var glyph, out var regular))
        {
            _icon.Visible = false;
            return;
        }

        _icon.Text = glyph;
        _icon.FontOverride = TguiIcons.Font(_fontSize, regular);
        _icon.Visible = true;
        TextLabel.Visible = !string.IsNullOrEmpty(TextLabel.Text);
        DrawModeChanged();
    }

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            DrawModeChanged();
        }
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // Базовый конструктор вызывает это до инициализации полей.
        if (_style == null || TextLabel == null)
            return;

        var transparent = _color.A <= 0.01f;
        _style.BackgroundColor = DrawMode switch
        {
            DrawModeEnum.Disabled => transparent ? Color.Transparent : Tgui.Disabled,
            DrawModeEnum.Hover => transparent ? Color.White.WithAlpha(0.1f) : Tgui.Lighten(_color, 0.08f),
            DrawModeEnum.Pressed => transparent ? Color.White.WithAlpha(0.18f) : Tgui.Lighten(_color, 0.15f),
            _ => _color,
        };
        TextLabel.FontColorOverride = DrawMode == DrawModeEnum.Disabled
            ? Color.FromHex(transparent ? "#888888" : "#bbbbbb")
            : Tgui.TextColor;
        if (_icon != null)
            _icon.FontColorOverride = TextLabel.FontColorOverride;
    }
}
