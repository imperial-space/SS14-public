using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Радар неба: позиция в центре, кольца дальности, бегущий луч развёртки, борта над позицией (красные — враг,
/// зелёные — свои, серые — пустые), разрывы последних секунд, курс башни и точка прицела. Клик — навести.
/// </summary>
public sealed class CadiaSkyRadarControl : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color Phosphor = Color.FromHex("#5aff78");
    private static readonly Color Dim = Color.FromHex("#1f4a2a");
    private static readonly Color Enemy = Color.FromHex("#ff3a2a");
    private static readonly Color Friendly = Color.FromHex("#5aff78");
    private static readonly Color Neutral = Color.FromHex("#9a9a8a");
    private static readonly Color AimColor = Color.FromHex("#ffd37a");

    public event Action<Vector2>? OnAim;

    private CadiaAntiAirBuiState? _state;
    private readonly Font _font;

    public CadiaSkyRadarControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 11);
    }

    public void SetState(CadiaAntiAirBuiState state)
    {
        _state = state;
    }

    private (Vector2 Center, float Radius, float Scale) Layout()
    {
        var center = PixelSize / 2f;
        var radius = MathF.Min(PixelSize.X, PixelSize.Y) / 2f - 14f;
        var range = _state is { Range: > 0 } ? _state.Range : 1f;
        return (center, radius, radius / range);
    }

    private Vector2 ToScreen(Vector2 offset)
    {
        var (center, _, scale) = Layout();
        return center + new Vector2(offset.X, -offset.Y) * scale;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || _state is not { Linked: true })
            return;
        var (center, _, scale) = Layout();
        var local = (args.RelativePixelPosition - center) / scale;
        OnAim?.Invoke(new Vector2(local.X, -local.Y));
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var (center, radius, scale) = Layout();

        if (_state is not { Linked: true })
        {
            var text = Loc.GetString("cadia-aa-ui-no-gun");
            handle.DrawString(_font, center - handle.GetDimensions(_font, text, 1f) / 2f, text, Enemy);
            return;
        }

        // Экран: кольца дальности, перекрестие, стороны света.
        handle.DrawCircle(center, radius, Color.FromHex("#061208"));
        for (var i = 1; i <= 4; i++)
            handle.DrawCircle(center, radius * i / 4f, Dim, false);
        handle.DrawLine(center - new Vector2(radius, 0), center + new Vector2(radius, 0), Dim);
        handle.DrawLine(center - new Vector2(0, radius), center + new Vector2(0, radius), Dim);
        foreach (var (label, dir) in new[] { (Loc.GetString("imperial-cadia-cadia-sky-radar-control-1"), new Vector2(0, -1)), (Loc.GetString("imperial-cadia-cadia-sky-radar-control-2"), new Vector2(0, 1)), (Loc.GetString("imperial-cadia-cadia-sky-radar-control-3"), new Vector2(1, 0)), (Loc.GetString("imperial-cadia-cadia-sky-radar-control-4"), new Vector2(-1, 0)) })
        {
            var size = handle.GetDimensions(_font, label, 1f);
            handle.DrawString(_font, center + dir * (radius + 7) - size / 2f, label, Phosphor.WithAlpha(0.6f));
        }

        // Луч развёртки с затухающим хвостом.
        var t = (float)(_timing.RealTime.TotalSeconds % 2.5 / 2.5) * MathF.Tau;
        for (var k = 0; k < 14; k++)
        {
            var a = t - k * 0.035f;
            var end = center + new Vector2(MathF.Sin(a), -MathF.Cos(a)) * radius;
            handle.DrawLine(center, end, Phosphor.WithAlpha(0.35f * (1 - k / 14f)));
        }

        // Курс башни сейчас и куда она доворачивает.
        var turret = _state.Aim.RotateVec(new Vector2(0, 1));
        handle.DrawLine(center, ToScreen(turret * _state.Range * 0.35f), AimColor);
        var target = _state.TargetAim.RotateVec(new Vector2(0, 1));
        handle.DrawLine(center, ToScreen(target * _state.Range), AimColor.WithAlpha(0.25f));

        // Борта: размер отметки — по размеру шаттла, номер этажа рядом.
        foreach (var contact in _state.Contacts)
        {
            var p = ToScreen(contact.Offset);
            var color = contact.Kind switch
            {
                CadiaAirContactKind.Enemy => Enemy,
                CadiaAirContactKind.Friendly => Friendly,
                _ => Neutral,
            };
            var r = Math.Max(5f, contact.Size * scale);
            handle.DrawCircle(p, r, color.WithAlpha(0.25f));
            handle.DrawCircle(p, r, color, false);
            var tag = Loc.GetString(contact.Level == 1 ? "cadia-aa-ui-tag-air" : "cadia-aa-ui-tag-orbit");
            handle.DrawString(_font, p + new Vector2(r + 3, -7), $"{contact.Name} [{tag}]", color);
        }

        // Разрывы: жёлтые — мимо, красные — в цель.
        foreach (var burst in _state.Bursts)
        {
            var p = ToScreen(burst.Offset);
            var color = burst.Hit ? Enemy : AimColor;
            handle.DrawLine(p - new Vector2(4, 4), p + new Vector2(4, 4), color);
            handle.DrawLine(p - new Vector2(4, -4), p + new Vector2(4, -4), color);
        }

        // Прицел.
        if (_state.AimPoint is { } aim)
        {
            var p = ToScreen(aim);
            handle.DrawCircle(p, 9, AimColor, false);
            handle.DrawLine(p - new Vector2(14, 0), p - new Vector2(5, 0), AimColor);
            handle.DrawLine(p + new Vector2(5, 0), p + new Vector2(14, 0), AimColor);
            handle.DrawLine(p - new Vector2(0, 14), p - new Vector2(0, 5), AimColor);
            handle.DrawLine(p + new Vector2(0, 5), p + new Vector2(0, 14), AimColor);
        }

        // Сама позиция.
        handle.DrawRect(UIBox2.FromDimensions(center - new Vector2(4), new Vector2(8)), Phosphor);
        if (_state.Firing)
            handle.DrawString(_font, new Vector2(8, 6), Loc.GetString("cadia-aa-ui-firing"), Enemy);
    }
}
