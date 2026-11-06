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

/// <summary>
/// Снимки камеры TG: по запросу сервера рисует область вокруг цели во внеэкранный viewport (свет — как в игре),
/// при ч/б печати переводит в оттенки серого, отдаёт PNG и миниатюру 8×8 (get_small_icon).
/// Миниатюра накладывается на значок фото.
/// </summary>
public sealed class ImperialPhotoCameraSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const int PixelsPerTile = EyeManager.PixelsPerMeter;
    private const string ThumbnailLayer = "imperial-photo-thumbnail";

    private sealed class Capture
    {
        public int Id;
        public bool Monochrome;
        public IClydeViewport Viewport = default!;
        public int Frames;
        public bool Copying;
    }

    private readonly List<Capture> _captures = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ImperialCameraCaptureRequestEvent>(OnRequest);
        SubscribeLocalEvent<ImperialPhotoComponent, AfterAutoHandleStateEvent>(OnPhotoState);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var capture in _captures)
            capture.Viewport.Dispose();
        _captures.Clear();
    }

    private void OnRequest(ImperialCameraCaptureRequestEvent msg)
    {
        var size = new Vector2i(Math.Clamp(msg.Width, 1, 7) * PixelsPerTile, Math.Clamp(msg.Height, 1, 7) * PixelsPerTile);
        var viewport = _clyde.CreateViewport(size, "imperial-camera");
        viewport.Eye = new Robust.Shared.Graphics.Eye
        {
            Position = msg.Center,
            Rotation = -msg.Rotation,
            DrawFov = false,
            DrawLight = true,
        };
        viewport.ClearColor = Color.Black;
        viewport.AutomaticRender = true;
        _captures.Add(new Capture { Id = msg.Id, Monochrome = msg.Monochrome, Viewport = viewport });
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var capture in _captures.ToArray())
        {
            // Пара кадров, чтобы viewport успел отрисоваться со светом.
            if (capture.Copying || ++capture.Frames < 3)
                continue;

            capture.Copying = true;
            capture.Viewport.RenderTarget.CopyPixelsToMemory<Rgba32>(image => Finish(capture, image));
        }
    }

    private void Finish(Capture capture, Image<Rgba32> image)
    {
        _captures.Remove(capture);
        capture.Viewport.Dispose();

        byte[] png;
        using (var stream = new MemoryStream())
        {
            image.SaveAsPng(stream);
            png = stream.ToArray();
        }

        RaiseNetworkEvent(new ImperialCameraCaptureResponseEvent(capture.Id, png, Array.Empty<byte>()));
        image.Dispose();
    }

    /// <summary>update_icon_state(): миниатюра снимка поверх значка фото.</summary>
    private void OnPhotoState(Entity<ImperialPhotoComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Thumbnail.Length != 8 * 8 * 4 || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        using var img = new Image<Rgba32>(8, 8);
        for (var i = 0; i < 64; i++)
            img[i % 8, i / 8] = new Rgba32(ent.Comp.Thumbnail[i * 4], ent.Comp.Thumbnail[i * 4 + 1], ent.Comp.Thumbnail[i * 4 + 2], ent.Comp.Thumbnail[i * 4 + 3]);
        var texture = _clyde.LoadTextureFromImage(img);
        if (_sprite.LayerMapTryGet((ent, sprite), ThumbnailLayer, out var index, false))
        {
            _sprite.LayerSetTexture((ent, sprite), index, texture);
            return;
        }

        index = _sprite.AddTextureLayer((ent, sprite), texture);
        _sprite.LayerMapSet((ent, sprite), ThumbnailLayer, index);
    }
}
