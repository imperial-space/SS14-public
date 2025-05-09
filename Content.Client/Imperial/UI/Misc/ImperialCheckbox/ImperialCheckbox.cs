using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.UI;


[Virtual]
public class ImperialCheckbox : BoxContainer
{
    public event Action<bool>? OnCheckboxToggled;

    public PanelContainer CheckboxButtonContainer;
    public TextureButton CheckboxButton;
    public Label TextLabel;


    private string _texturePath = "/Textures/Imperial/ImperialPass/Icons/CheckMark/check_mark.svg.196dpi.png";
    private bool _toggled = false;

    [ViewVariables]
    public string TexturePath
    {
        get => _texturePath;
        set
        {
            _texturePath = value;

            if (_toggled) CheckboxButton.TextureNormal = GetTextureFromPath(value);
        }
    }

    [ViewVariables]
    public string? LabelText { get => TextLabel.Text; set => TextLabel.Text = value; }

    [ViewVariables]
    public bool Toggled { get => _toggled; set => OnCheckboxPressed(); }

    [ViewVariables]
    public StyleBoxFlat CheckboxBackground
    {
        get => (StyleBoxFlat)CheckboxButtonContainer.PanelOverride!;
        set => CheckboxButtonContainer.PanelOverride = value;
    }

    public ImperialCheckbox()
    {
        CheckboxButtonContainer = new PanelContainer()
        {
            PanelOverride = new StyleBoxFlat
            {
                BorderColor = Color.FromHex("#323232"),
                BackgroundColor = Color.FromHex("#1E1E1E"),
                BorderThickness = new Thickness(1)
            },
            MaxSize = new Vector2(20, 20),
            SetSize = new Vector2(20, 20),
            VerticalAlignment = VAlignment.Center
        };

        CheckboxButton = new TextureButton()
        {
            VerticalAlignment = VAlignment.Stretch,
            HorizontalAlignment = HAlignment.Stretch,
            MaxSize = new Vector2(15, 15)
        };

        TextLabel = new Label()
        {
            Margin = new Thickness(15, 0, 0, 0),
            Align = Label.AlignMode.Center
        };

        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;

        CheckboxButtonContainer.AddChild(CheckboxButton);
        AddChild(CheckboxButtonContainer);
        AddChild(TextLabel);

        CheckboxButton.OnPressed += (_) => OnCheckboxPressed();
    }

    private void OnCheckboxPressed()
    {
        _toggled = !_toggled;
        CheckboxButton.TextureNormal = _toggled ? GetTextureFromPath(_texturePath) : null;

        OnCheckboxToggled?.Invoke(_toggled);
    }

    #region Helpers

    private Texture GetTextureFromPath(string path) => new SpriteSpecifier.Texture(new(path)).DirFrame0().Default;

    #endregion
}
