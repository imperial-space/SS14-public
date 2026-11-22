using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client.Imperial.DeimonFly.DeathNote.UI;

/// <summary>
/// Creates the same subdued page background used by the standard BookBase UI.
/// Keeping this in one place prevents writable and intentionally blank pages
/// from drifting apart visually.
/// </summary>
internal static class DeathNotePaperStyle
{
    private const string BookBackground =
        "/Textures/Interface/Paper/paper_background_book.svg.96dpi.png";

    public static StyleBoxTexture Create()
    {
        var resources = IoCManager.Resolve<IResourceCache>();
        return new StyleBoxTexture
        {
            Texture = resources.GetResource<TextureResource>(BookBackground),
            PatchMarginLeft = 23f,
            PatchMarginBottom = 16f,
            PatchMarginRight = 14f,
            PatchMarginTop = 15f,
        };
    }
}
