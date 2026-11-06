using System.IO;
using System.Numerics;
using Content.Shared.Imperial.Vending;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Imperial.Vending;

/// <summary>show(): окно со снимком 480 px и надписью на обороте.</summary>
public sealed class ImperialPhotoBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IClyde _clyde = default!;

    private DefaultWindow? _window;
    private TextureRect _image = default!;
    private RichTextLabel _scribble = default!;

    protected override void Open()
    {
        base.Open();
        IoCManager.InjectDependencies(this);

        _window = this.CreateWindow<DefaultWindow>();
        _window.SetSize = new Vector2(500, 540);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        _image = new TextureRect
        {
            MinSize = new Vector2(480, 480),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            CanShrink = true,
            VerticalExpand = true,
        };
        root.AddChild(_image);
        _scribble = new RichTextLabel { Margin = new Thickness(4) };
        root.AddChild(_scribble);
        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialPhotoState photo || _window == null)
            return;

        _window.Title = photo.Name;
        if (photo.Image.Length > 0)
        {
            using var stream = new MemoryStream(photo.Image);
            _image.Texture = _clyde.LoadTextureFromPNGStream(stream, "imperial-photo");
        }

        _scribble.Visible = !string.IsNullOrEmpty(photo.Scribble);
        if (photo.Scribble != null)
            _scribble.SetMessage(Robust.Shared.Utility.FormattedMessage.FromMarkupPermissive(
                Loc.GetString("imperial-photo-scribble", ("text", Robust.Shared.Utility.FormattedMessage.EscapeText(photo.Scribble)))));
    }
}
