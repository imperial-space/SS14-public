using System.Linq;
using System.Numerics;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// set_starlight(): за 9 шагов по 8 с звёзды тускнеют (насыщенность 0.4, яркость 0.5) и краснеют до #c21d57;
/// при провале — 4 шага по 8 с обратно. Рисуется под миром, поэтому окрашивает только космос.
/// </summary>
public sealed class CultStarlightOverlay : Overlay
{
    private readonly IGameTiming _timing;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    private static readonly Color Mid = new(0f, 0f, 0f, 0.5f);
    private static readonly Color End = Color.FromHex("#c21d5780");

    private Color _from = Color.Transparent;
    private Color _current = Color.Transparent;
    private bool _summoning;
    private TimeSpan _start;
    private bool _active;

    public CultStarlightOverlay(IGameTiming timing)
    {
        _timing = timing;
        ZIndex = 1;
    }

    public void Start(bool summoning)
    {
        _from = _current;
        _summoning = summoning;
        _start = _timing.RealTime;
        _active = true;
    }

    public void Reset()
    {
        _current = Color.Transparent;
        _active = false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_active)
        {
            var t = (float) (_timing.RealTime - _start).TotalSeconds;
            if (_summoning)
            {
                // hsv_gradient(i, 1, start, 3, mid, 6, mid, 9, end), шаги по 8 с.
                var step = Math.Clamp(MathF.Floor(t / 8f) + 1, 1, 9);
                _current = step <= 3
                    ? Color.InterpolateBetween(_from, Mid, (step - 1) / 2f)
                    : step <= 6 ? Mid : Color.InterpolateBetween(Mid, End, (step - 6) / 3f);
            }
            else
            {
                var step = Math.Clamp(MathF.Floor(t / 8f) + 1, 1, 4);
                _current = Color.InterpolateBetween(_from, Color.Transparent, step / 4f);
                if (step >= 4)
                    _active = false;
            }
        }

        if (_current.A <= 0.001f)
            return;
        args.WorldHandle.DrawRect(args.WorldBounds, _current);
    }
}
