using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.UI;


/// <summary>
/// Label that allows you to place two textures on the edges of the text
/// </summary>
[Virtual]
public class ImagedRichLabel : BoxContainer
{
    private Vector2 _leftTextureScale = Vector2.One;
    private Vector2 _rightTextureScale = Vector2.One;

    private ResPath _leftTexturePath = ResPath.Empty;
    private ResPath _rightTexturePath = ResPath.Empty;


    [ViewVariables]
    public Vector2? LeftTextureScale { get => _leftTextureScale; set { _leftTextureScale = value ?? Vector2.One; Rerender(); } }
    [ViewVariables]
    public Vector2? RightTextureScale { get => _leftTextureScale; set { _rightTextureScale = value ?? Vector2.One; Rerender(); } }
    [ViewVariables]
    public string? LeftTexturePath { get => _leftTexturePath.CanonPath; set { _leftTexturePath = value != null ? new ResPath(value) : ResPath.Empty; Rerender(); } }
    [ViewVariables]
    public string? RightTexturePath { get => _rightTexturePath.CanonPath; set { _rightTexturePath = value != null ? new ResPath(value) : ResPath.Empty; Rerender(); } }


    public RichTextLabel CenteredLabel { get; set; }
    public TextureRect LeftTexture { get; set; }
    public TextureRect RightTexture { get; set; }


    private bool HaveLeftTexture => _leftTexturePath != ResPath.Empty;

    private bool HaveRightTexture => _rightTexturePath != ResPath.Empty;


    public ImagedRichLabel()
    {
        CenteredLabel = new RichTextLabel()
        {
            VerticalAlignment = VAlignment.Center,
            Access = AccessLevel.Public
        };

        LeftTexture = new TextureRect()
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            Stretch = TextureRect.StretchMode.KeepAspect,
            Texture = HaveLeftTexture ? new SpriteSpecifier.Texture(_leftTexturePath).DirFrame0().Default : null,
            Visible = HaveLeftTexture,
            Access = AccessLevel.Public,
            Name = "LeftTexture"
        };

        RightTexture = new TextureRect()
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0),
            Stretch = TextureRect.StretchMode.KeepAspect,
            Texture = HaveRightTexture ? new SpriteSpecifier.Texture(_rightTexturePath).DirFrame0().Default : null,
            Visible = HaveRightTexture,
            Access = AccessLevel.Public,
            Name = "RightTexture"
        };

        AddChild(LeftTexture);
        AddChild(CenteredLabel);
        AddChild(RightTexture);
    }

    #region Helpers

    private void Rerender()
    {
        LeftTexture.Visible = HaveLeftTexture;
        LeftTexture.Texture = HaveLeftTexture ? new SpriteSpecifier.Texture(_leftTexturePath).DirFrame0().Default : null;
        LeftTexture.TextureScale = _leftTextureScale;

        RightTexture.Visible = HaveRightTexture;
        RightTexture.Texture = HaveRightTexture ? new SpriteSpecifier.Texture(_rightTexturePath).DirFrame0().Default : null;
        RightTexture.TextureScale = _rightTextureScale;
    }

    #endregion
}
