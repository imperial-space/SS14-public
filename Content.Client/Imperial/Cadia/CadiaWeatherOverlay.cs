using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Погода под открытым небом. Туман и гарь закрывают края экрана, пепел ещё и падает хлопьями, которые держатся за мир, а не за экран.
/// </summary>
public sealed class CadiaWeatherOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private const float RampSeconds = 8f;
    private const float MaskScale = 1.25f;
    private const int Flakes = 140;
    private const float PixelsPerTile = 32f;

    private readonly Texture _mask;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaWeatherOverlay()
    {
        IoCManager.InjectDependencies(this);
        _mask = IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>("/Textures/Imperial/Cadia/War/Intro/fog_mask.png").Texture;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        CadiaWarStateComponent? war = null;
        var query = _entMan.EntityQueryEnumerator<CadiaWarStateComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            war = comp;
            break;
        }
        if (war == null || war.Weather == CadiaWeather.Clear || _player.LocalEntity is not { } local)
            return;
        if (_entMan.GetComponent<TransformComponent>(local).MapUid is not { } map || _entMan.HasComponent<CadiaVehicleInteriorComponent>(map) ||
            !_entMan.TryGetComponent<CadiaFlightLevelComponent>(map, out var level) || level.Level != CadiaFlightLevel.Surface)
            return;

        var now = _timing.CurTime;
        var ramp = Math.Clamp(Math.Min((float)(now - war.WeatherStart).TotalSeconds, (float)(war.WeatherEnd - now).TotalSeconds) / RampSeconds, 0f, 1f);
        if (ramp <= 0f)
            return;

        var (tint, density) = war.Weather switch
        {
            CadiaWeather.Fog => (Color.FromHex("#b4b8ae"), 0.94f),
            CadiaWeather.Ash => (Color.FromHex("#6a645c"), 0.5f),
            _ => (Color.FromHex("#2e2a24"), 0.82f),
        };
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var side = Math.Max(bounds.Width, bounds.Height) * MaskScale;
        var center = new Vector2(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
        var rect = UIBox2.FromDimensions(center - new Vector2(side / 2f), new Vector2(side));
        handle.DrawTextureRect(_mask, rect, tint.WithAlpha(density * ramp));

        // Квадрат маски шире экрана, поэтому на вытянутых мониторах края тоже закрыты.
        if (war.Weather != CadiaWeather.Ash)
            return;
        var eye = _eye.CurrentEye.Position.Position * PixelsPerTile;
        var time = (float)now.TotalSeconds;
        for (var i = 0; i < Flakes; i++)
        {
            var seedX = (i * 7919 % 1000) / 1000f;
            var seedY = (i * 104729 % 1000) / 1000f;
            var speed = 16f + (i * 13 % 10) * 2f;
            var sway = MathF.Sin(time * 0.7f + i) * 10f;
            var x = Wrap(seedX * bounds.Width + sway - time * 6f - eye.X, bounds.Width);
            var y = Wrap(seedY * bounds.Height + time * speed + eye.Y, bounds.Height);
            var dot = 1.5f + (i % 3) * 0.75f;
            var color = (i % 5 == 0 ? Color.FromHex("#e8843a") : Color.FromHex("#c8c4bc")).WithAlpha(0.75f * ramp);
            handle.DrawRect(UIBox2.FromDimensions(new Vector2(bounds.Left + x, bounds.Top + y), new Vector2(dot)), color);
        }
    }

    private static float Wrap(float value, float size)
    {
        var result = value % size;
        return result < 0f ? result + size : result;
    }
}
