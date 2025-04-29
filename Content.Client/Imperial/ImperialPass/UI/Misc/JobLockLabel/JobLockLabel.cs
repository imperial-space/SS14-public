using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Pass.UI;


public sealed partial class JobLockLabel : Control
{
    public const string LabelSubTextPassLower = "LabelSubTextPassLower";

    private SpriteSystem _spriteSystem = default!;


    private Label _label = default!;
    private TextureRect _textureRect = default!;


    private string _text = "";
    private ResPath _texturePath = ResPath.Empty;


    [ViewVariables]
    public string? Text { set { _text = value ?? ""; Rerender(); } }

    [ViewVariables]
    public ResPath? TextEndTexturePath { set { _texturePath = value ?? ResPath.Empty; Rerender(); } }

    [ViewVariables]
    public Vector2 TextureScale { get => _textureRect.TextureScale; set => _textureRect.TextureScale = value; }


    public JobLockLabel()
    {
        _spriteSystem = IoCManager.Resolve<EntityManager>().System<SpriteSystem>();

        _label = new Label()
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            Align = Label.AlignMode.Center,
            StyleClasses = { LabelSubTextPassLower },
        };

        _textureRect = new TextureRect()
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            TextureScale = new Vector2(0.70f, 0.70f),
            Margin = new Thickness(10, 0, 0, 0),
            Stretch = TextureRect.StretchMode.KeepAspect,
        };

        var container = new BoxContainer()
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(15, 0, 0, 0),
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children = { _label, _textureRect }
        };

        Children.Add(container);
    }

    #region Helpers

    private void Rerender()
    {
        _label.Text = _text;
        _textureRect.Texture = _spriteSystem.Frame0(new SpriteSpecifier.Texture(_texturePath));
    }

    #endregion
}
