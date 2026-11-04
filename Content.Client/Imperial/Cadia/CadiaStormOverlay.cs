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

/// <summary>Варп-шторм под открытым небом: красный ливень, тёмные края при шёпоте, вспышки при прорыве демонов.</summary>
public sealed class CadiaStormOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int Drops = 160;
    private const float DropLength = 16f;
    private const float RampSeconds = 3f;
    private const int VignetteSteps = 8;
    private const float VignetteDepth = 150f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaStormOverlay()
    {
        IoCManager.InjectDependencies(this);
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
        if (war == null || war.Storm == CadiaWarpStorm.None || _player.LocalEntity is not { } local)
            return;
        var map = _entMan.GetComponent<TransformComponent>(local).MapUid;
        if (map is not { } mapUid || _entMan.HasComponent<CadiaVehicleInteriorComponent>(mapUid) ||
            _entMan.TryGetComponent<CadiaFlightLevelComponent>(mapUid, out var level) && level.Level == CadiaFlightLevel.Orbit)
            return;

        var now = _timing.CurTime;
        var left = (float) (war.StormEnd - now).TotalSeconds;
        if (left <= 0f)
            return;
        var ramp = Math.Clamp(left / RampSeconds, 0f, 1f);
        var time = (float) now.TotalSeconds;
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;

        switch (war.Storm)
        {
            case CadiaWarpStorm.BloodRain:
                handle.DrawRect(bounds, Color.FromHex("#5a0a12").WithAlpha(0.22f * ramp));
                for (var i = 0; i < Drops; i++)
                {
                    // Псевдослучайно, но одинаково из кадра в кадр: иначе капли будут дрожать, а не падать.
                    var seedX = (i * 7919 % 1000) / 1000f;
                    var seedY = (i * 104729 % 1000) / 1000f;
                    var speed = 0.55f + (i * 31 % 10) * 0.04f;
                    var fall = (seedY + time * speed) % 1f;
                    var x = bounds.Left + seedX * bounds.Width - fall * 60f;
                    var y = bounds.Top + fall * bounds.Height;
                    handle.DrawLine(new Vector2(x, y), new Vector2(x - 4f, y + DropLength), Color.FromHex("#b0121f").WithAlpha(0.55f * ramp));
                }
                break;
            case CadiaWarpStorm.Whispers:
                var pulse = 0.55f + 0.3f * MathF.Sin(time * 1.3f);
                for (var i = 0; i < VignetteSteps; i++)
                {
                    var inset = VignetteDepth * i / VignetteSteps;
                    var band = VignetteDepth / VignetteSteps;
                    var alpha = pulse * ramp * (1f - (float) i / VignetteSteps) * 0.5f;
                    var color = Color.FromHex("#14051e").WithAlpha(alpha);
                    handle.DrawRect(new UIBox2(bounds.Left, bounds.Top + inset, bounds.Right, bounds.Top + inset + band), color);
                    handle.DrawRect(new UIBox2(bounds.Left, bounds.Bottom - inset - band, bounds.Right, bounds.Bottom - inset), color);
                    handle.DrawRect(new UIBox2(bounds.Left + inset, bounds.Top, bounds.Left + inset + band, bounds.Bottom), color);
                    handle.DrawRect(new UIBox2(bounds.Right - inset - band, bounds.Top, bounds.Right - inset, bounds.Bottom), color);
                }
                break;
            case CadiaWarpStorm.DaemonSurge:
                var flash = MathF.Max(0f, MathF.Sin(time * 5f) * MathF.Sin(time * 1.7f));
                handle.DrawRect(bounds, Color.FromHex("#7a1fb0").WithAlpha(0.28f * flash * ramp));
                break;
        }
    }
}
