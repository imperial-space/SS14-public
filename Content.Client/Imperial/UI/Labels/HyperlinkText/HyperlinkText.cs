using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.Label;

namespace Content.Client.Imperial.UI;

/// <summary>
///     Most common button type that draws text in a fancy box.
/// </summary>
[Virtual]
public class HyperlinkText : ContainerButton
{
    [Dependency] private readonly IUriOpener _uriOpener = default!;


    public Label Label { get; }
    public HLine HLinee { get; }


    private string? _uri;

    private Color _linkColor = Color.FromHex("#FFF");
    private Color _normal = Color.FromHex("#FFF");
    private Color _hover = Color.FromHex("#d2a753");
    private Color _pressed = Color.FromHex("#e4cfa5");
    private Color _disabled = Color.FromHex("#313131");

    /// <summary>
    ///     How to align the text inside the button.
    /// </summary>
    [ViewVariables]
    public AlignMode TextAlign { get => Label.Align; set => Label.Align = value; }

    /// <summary>
    ///     If true, the button will allow shrinking and clip text
    ///     to prevent the text from going outside the bounds of the button.
    ///     If false, the minimum size will always fit the contained text.
    /// </summary>
    [ViewVariables]
    public bool ClipText { get => Label.ClipText; set => Label.ClipText = value; }

    /// <summary>
    ///     The text displayed by the button.
    /// </summary>
    [ViewVariables]
    public string? Text { get => Label.Text; set => Label.Text = value; }

    [ViewVariables]
    public string? Href { get => _uri; set => _uri = value; }



    [ViewVariables]
    public Color? OutlineColor { get => HLinee.Color; set => HLinee.Color = value; }

    [ViewVariables]
    public Color? TextColorOverride { get => Label.FontColorOverride; set => Label.FontColorOverride = value; }

    [ViewVariables]
    public Font? TextFontOverride { get => Label.FontOverride; set => Label.FontOverride = value; }


    [ViewVariables]
    public Color NormalLinkColor { get => _normal; set => _normal = value; }

    [ViewVariables]
    public Color HoverLinkColor { get => _hover; set => _hover = value; }

    [ViewVariables]
    public Color PressedLinkColor { get => _pressed; set => _pressed = value; }

    [ViewVariables]
    public Color DisabledLinkColor { get => _disabled; set => _disabled = value; }


    [ViewVariables]
    public Color LinkColor
    {
        get => _linkColor;
        set
        {
            _linkColor = value;

            if (Label != null) TextColorOverride = value;
            if (HLinee != null) OutlineColor = value;
        }
    }

    public HyperlinkText()
    {
        IoCManager.InjectDependencies(this);

        HLinee = new HLine()
        {
            Thickness = 1,
            Color = LinkColor
        };
        Label = new Label
        {
            StyleClasses = { "LabelSubTextPassMedium" }
        };
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { Label, HLinee }
        };

        OnPressed += OnLinkPressed;

        AddChild(box);
    }

    protected override void DrawModeChanged()
    {
        switch (DrawMode)
        {
            case DrawModeEnum.Normal:
                ModulateSelfOverride = _normal;
                LinkColor = _normal;

                break;
            case DrawModeEnum.Pressed:
                ModulateSelfOverride = _pressed;
                LinkColor = _pressed;

                break;
            case DrawModeEnum.Hover:
                ModulateSelfOverride = _hover;
                LinkColor = _hover;

                break;
            case DrawModeEnum.Disabled:
                ModulateSelfOverride = _disabled;
                LinkColor = _disabled;

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void OnLinkPressed(ButtonEventArgs args)
    {
        if (string.IsNullOrEmpty(_uri)) return;

        _uriOpener.OpenUri(_uri);
    }
}
