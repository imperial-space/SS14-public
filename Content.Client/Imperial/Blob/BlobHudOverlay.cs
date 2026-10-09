using System.Numerics;
using Content.Shared.Imperial.Blob.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Blob;

/// <summary>
/// HUD блоба: ресурсы и здоровье ядра (blob_power, corehealth), прогресс и таймеры (Stat panel SS13).
/// </summary>
public sealed class BlobHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color PowerColor = Color.FromHex("#e36600");
    private static readonly Color HealthColor = Color.FromHex("#82ed00");

    private readonly Font _bigFont;
    private readonly Font _font;
    private readonly Texture _powerIcon;
    private readonly Texture _healthIcon;
    private const float IconSize = 64f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public BlobHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _bigFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 16);
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
        var sprite = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();
        var rsi = new ResPath("/Textures/Imperial/blob/blob.rsi");
        _powerIcon = sprite.Frame0(new SpriteSpecifier.Rsi(rsi, "block"));
        _healthIcon = sprite.Frame0(new SpriteSpecifier.Rsi(rsi, "corehealth"));
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local)
            return;

        var handle = args.ScreenHandle;
        var x = args.ViewportBounds.Right - 230f;
        var y = args.ViewportBounds.Top + args.ViewportBounds.Height * 0.35f;

        if (_entMan.TryGetComponent<BlobOvermindComponent>(local, out var overmind))
        {
            // HUD_MOB_HEALTH и HUD_BLOB_POWER_DISPLAY: иконки с числом поверх.
            var iconX = args.ViewportBounds.Right - IconSize - 8;
            if (overmind.CoreHealth >= 0)
                DrawIcon(handle, _healthIcon, new Vector2(iconX, y - IconSize - 4), overmind.CoreHealth + "%", HealthColor);
            var points = float.IsPositiveInfinity(overmind.Points) ? "∞" : ((int) overmind.Points).ToString();
            DrawIcon(handle, _powerIcon, new Vector2(iconX, y), points, PowerColor);
            y += IconSize + 8;

            if (overmind.CoreHealth >= 0)
            {
                handle.DrawString(_font, new Vector2(x, y), Loc.GetString("blob-hud-progress", ("count", overmind.BlobCount), ("win", overmind.WinCount)), Color.White);
                y += 16;
            }

            if (overmind.FreeRerolls > 0)
            {
                handle.DrawString(_font, new Vector2(x, y), Loc.GetString("blob-hud-rerolls", ("count", overmind.FreeRerolls)), Color.White);
                y += 16;
            }

            if (!overmind.Placed)
            {
                var now = _timing.CurTime;
                var manual = overmind.ManualPlaceTime - now;
                if (manual > TimeSpan.Zero)
                {
                    handle.DrawString(_font, new Vector2(x, y), Loc.GetString("blob-hud-manual", ("seconds", Math.Round(manual.TotalSeconds, 1))), Color.White);
                    y += 16;
                }

                var auto = overmind.AutoPlaceTime - now;
                handle.DrawString(_font, new Vector2(x, y), Loc.GetString("blob-hud-auto", ("seconds", Math.Max(0, Math.Round(auto.TotalSeconds, 1)))), Color.White);
            }

            return;
        }

        // Блоббернаут видит здоровье ядра своего оверманда.
        if (_entMan.TryGetComponent<BlobMobComponent>(local, out var mob) && mob.Type == BlobMobType.Blobbernaut && mob.OvermindCoreHealth >= 0)
            DrawIcon(handle, _healthIcon, new Vector2(args.ViewportBounds.Right - IconSize - 8, y), mob.OvermindCoreHealth + "%", HealthColor);
    }

    private void DrawIcon(DrawingHandleScreen handle, Texture icon, Vector2 pos, string text, Color color)
    {
        handle.DrawTextureRect(icon, UIBox2.FromDimensions(pos, new Vector2(IconSize, IconSize)));
        var size = handle.GetDimensions(_bigFont, text, 1f);
        handle.DrawString(_bigFont, pos + new Vector2((IconSize - size.X) / 2 + 6, (IconSize - size.Y) / 2), text, color);
    }
}

public sealed class BlobHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private BlobHudOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();
        _hud = new BlobHudOverlay();
        _overlay.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_hud != null)
            _overlay.RemoveOverlay(_hud);
    }
}
