using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Голокарта: этаж поля зелёной гололитической проекцией, нанесённое сторонами, живые отметки.
/// Колесо — масштаб к курсору, ПКМ — перетаскивание, ЛКМ — инструмент.
/// </summary>
public sealed class CadiaHoloTableMapControl : Control
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public event Action<CadiaHoloElement>? OnAdd;
    public event Action<int>? OnErase;
    public event Action<Vector2, CadiaFlightLevel>? OnOrder;

    public int OrderSquad;
    public CadiaSquadOrder OrderType = CadiaSquadOrder.Advance;

    public CadiaHoloTool Tool = CadiaHoloTool.Line;
    public CadiaHoloMarkerType MarkerType = CadiaHoloMarkerType.Target;
    public CadiaBlueprintKind BlueprintKind = CadiaBlueprintKind.Sandbags;
    public CadiaHoloLayer ActiveLayer = CadiaHoloLayer.Assault;
    public CadiaFlightLevel Level = CadiaFlightLevel.Surface;
    public Color DrawColor = CadiaHoloTableWindow.Palette[0];
    public readonly HashSet<CadiaHoloLayer> VisibleLayers = new(Enum.GetValues<CadiaHoloLayer>());
    public bool ReadOnly;

    private static readonly Color Holo = Color.FromHex("#5aff78");
    private static readonly Color BlueprintColor = Color.FromHex("#4ad8ff");

    private CadiaHoloTableState? _state;
    private readonly Dictionary<CadiaFlightLevel, (Vector2i Origin, int Width, int Height, Texture Texture)> _tiles = new();
    private readonly Font _font;
    private readonly Font _small;

    private Vector2 _center;
    private float _scale = 6f;
    private bool _fitted;
    private Vector2? _dragStart;
    private Vector2? _panFrom;
    private Vector2 _mouse;

    public CadiaHoloTableMapControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 12);
        _small = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
    }

    public void SetState(CadiaHoloTableState state)
    {
        if (_state == null)
            _center = state.TablePosition;
        _state = state;
        if (!state.Levels.Contains(Level) && state.Levels.Count > 0)
            Level = state.Levels[0];
    }

    public void SetTiles(CadiaHoloTilesMessage msg)
    {
        if (msg.Width <= 0 || msg.Height <= 0 || msg.Tiles.Length != msg.Width * msg.Height)
            return;
        using var image = new Image<Rgba32>(msg.Width, msg.Height);
        for (var y = 0; y < msg.Height; y++)
        {
            for (var x = 0; x < msg.Width; x++)
            {
                image[x, msg.Height - 1 - y] = msg.Tiles[y * msg.Width + x] switch
                {
                    1 => new Rgba32(18, 60, 30, 255),
                    2 => new Rgba32(110, 230, 140, 255),
                    3 => new Rgba32(230, 210, 90, 255),
                    _ => new Rgba32(2, 8, 4, 255),
                };
            }
        }
        _tiles[msg.Level] = (msg.Origin, msg.Width, msg.Height, _clyde.LoadTextureFromImage(image));
        if (!_fitted && msg.Level == Level)
        {
            _fitted = true;
            _center = msg.Origin + new Vector2(msg.Width, msg.Height) / 2f;
            _scale = MathF.Max(2f, MathF.Min(PixelSize.X / (float)msg.Width, PixelSize.Y / (float)msg.Height) * 0.95f);
        }
    }

    // ─── Координаты ───

    private Vector2 ToScreen(Vector2 world) => PixelSize / 2f + new Vector2(world.X - _center.X, _center.Y - world.Y) * _scale;
    private Vector2 ToWorld(Vector2 screen) => new(_center.X + (screen.X - PixelSize.X / 2f) / _scale, _center.Y - (screen.Y - PixelSize.Y / 2f) / _scale);

    // ─── Ввод ───

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (_state == null)
            return;
        if (args.Function == EngineKeyFunctions.UIRightClick || (ReadOnly && args.Function == EngineKeyFunctions.UIClick))
        {
            _panFrom = args.RelativePixelPosition;
            args.Handle();
            return;
        }
        if (args.Function != EngineKeyFunctions.UIClick)
            return;
        args.Handle();

        var world = ToWorld(args.RelativePixelPosition);
        switch (Tool)
        {
            case CadiaHoloTool.Line:
            case CadiaHoloTool.Arrow:
            case CadiaHoloTool.Circle:
                _dragStart = world;
                break;
            case CadiaHoloTool.Marker:
                Add(CadiaHoloShape.Marker, world, world, (byte)MarkerType);
                break;
            case CadiaHoloTool.Text:
                Add(CadiaHoloShape.Text, world, world, 0);
                break;
            case CadiaHoloTool.Blueprint:
                Add(CadiaHoloShape.Blueprint, world, world, (byte)BlueprintKind);
                break;
            case CadiaHoloTool.Eraser:
                if (Nearest(args.RelativePixelPosition) is { } id)
                    OnErase?.Invoke(id);
                break;
            case CadiaHoloTool.Order:
                OnOrder?.Invoke(world, Level);
                break;
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIRightClick || (ReadOnly && args.Function == EngineKeyFunctions.UIClick))
        {
            _panFrom = null;
            return;
        }
        if (args.Function != EngineKeyFunctions.UIClick || _dragStart is not { } start)
            return;
        _dragStart = null;
        var end = ToWorld(args.RelativePixelPosition);
        if ((end - start).Length() * _scale < 6f)
            return;
        var shape = Tool switch
        {
            CadiaHoloTool.Arrow => CadiaHoloShape.Arrow,
            CadiaHoloTool.Circle => CadiaHoloShape.Circle,
            _ => CadiaHoloShape.Line,
        };
        Add(shape, start, end, 0);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _mouse = args.RelativePixelPosition;
        if (_panFrom is { } from)
        {
            var delta = args.RelativePixelPosition - from;
            _center -= new Vector2(delta.X, -delta.Y) / _scale;
            _panFrom = args.RelativePixelPosition;
        }
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        var before = ToWorld(args.RelativePixelPosition);
        _scale = Math.Clamp(_scale * (args.Delta.Y > 0 ? 1.2f : 1f / 1.2f), 1.5f, 40f);
        var after = ToWorld(args.RelativePixelPosition);
        _center += before - after;
        args.Handle();
    }

    private void Add(CadiaHoloShape shape, Vector2 a, Vector2 b, byte kind)
    {
        OnAdd?.Invoke(new CadiaHoloElement
        {
            Shape = shape,
            Layer = shape == CadiaHoloShape.Blueprint ? CadiaHoloLayer.Defense : ActiveLayer,
            Level = Level,
            Color = DrawColor,
            A = a,
            B = b,
            Kind = kind,
        });
    }

    private int? Nearest(Vector2 screen)
    {
        if (_state == null)
            return null;
        var best = 14f;
        int? result = null;
        foreach (var e in _state.Elements)
        {
            if (e.Level != Level || !VisibleLayers.Contains(e.Layer))
                continue;
            var a = ToScreen(e.A);
            var b = ToScreen(e.B);
            var d = e.Shape switch
            {
                CadiaHoloShape.Line or CadiaHoloShape.Arrow => DistanceToSegment(screen, a, b),
                CadiaHoloShape.Circle => MathF.Abs((screen - a).Length() - (b - a).Length()),
                _ => (screen - a).Length(),
            };
            if (d < best)
            {
                best = d;
                result = e.Id;
            }
        }
        return result;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 0.01f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return (p - (a + ab * t)).Length();
    }

    // ─── Отрисовка ───

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(PixelSizeBox, Color.FromHex("#020604"));
        if (_state == null)
            return;

        var t = (float)_timing.RealTime.TotalSeconds;
        var pulse = 0.6f + 0.4f * MathF.Sin(t * 4f);

        if (_tiles.TryGetValue(Level, out var tiles))
        {
            var bl = ToScreen(tiles.Origin);
            var tr = ToScreen(tiles.Origin + new Vector2(tiles.Width, tiles.Height));
            handle.DrawTextureRect(tiles.Texture, new UIBox2(bl.X, tr.Y, tr.X, bl.Y));
        }
        else
        {
            var text = Loc.GetString("cadia-holo-ui-loading");
            handle.DrawString(_font, (PixelSize - handle.GetDimensions(_font, text, 1f)) / 2f, text, Holo);
        }

        // Координатная сетка проектора каждые 10 тайлов.
        var grid = Color.FromHex("#1e5a2a30");
        var topLeft = ToWorld(Vector2.Zero);
        var bottomRight = ToWorld(PixelSize);
        for (var x = MathF.Floor(topLeft.X / 10) * 10; x <= bottomRight.X; x += 10)
            handle.DrawLine(new Vector2(ToScreen(new Vector2(x, 0)).X, 0), new Vector2(ToScreen(new Vector2(x, 0)).X, PixelSize.Y), grid);
        for (var y = MathF.Floor(bottomRight.Y / 10) * 10; y <= topLeft.Y; y += 10)
            handle.DrawLine(new Vector2(0, ToScreen(new Vector2(0, y)).Y), new Vector2(PixelSize.X, ToScreen(new Vector2(0, y)).Y), grid);

        // Развёртка гололита.
        var sweep = PixelSize.Y * (t % 4f / 4f);
        handle.DrawRect(new UIBox2(0, sweep - 2, PixelSize.X, sweep + 2), Holo.WithAlpha(0.06f));

        foreach (var e in _state.Elements)
        {
            if (e.Level == Level && VisibleLayers.Contains(e.Layer))
                DrawElement(handle, e, pulse);
        }

        foreach (var live in _state.Live)
        {
            if (live.Level == Level)
                DrawLive(handle, live, pulse);
        }

        // Стол — «вы здесь».
        if (_state.Levels.FirstOrDefault() == Level || Level == CadiaFlightLevel.Surface)
        {
            var p = ToScreen(_state.TablePosition);
            handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(5), new Vector2(10)), Color.FromHex("#e8c66a"), false);
        }

        // Предпросмотр того, что тянется мышью.
        if (_dragStart is { } start)
        {
            var preview = new CadiaHoloElement
            {
                Shape = Tool switch { CadiaHoloTool.Arrow => CadiaHoloShape.Arrow, CadiaHoloTool.Circle => CadiaHoloShape.Circle, _ => CadiaHoloShape.Line },
                Color = DrawColor.WithAlpha(0.6f),
                A = start,
                B = ToWorld(_mouse),
            };
            DrawElement(handle, preview, pulse);
        }

        // Координаты под курсором.
        var mouseWorld = ToWorld(_mouse);
        handle.DrawString(_small, new Vector2(8, 6), $"{(int)MathF.Floor(mouseWorld.X)}, {(int)MathF.Floor(mouseWorld.Y)}", Holo);
    }

    private void Thick(DrawingHandleScreen handle, Vector2 a, Vector2 b, Color color)
    {
        var dir = b - a;
        var normal = dir.LengthSquared() < 0.01f ? Vector2.Zero : new Vector2(-dir.Y, dir.X) / dir.Length();
        handle.DrawLine(a, b, color);
        handle.DrawLine(a + normal, b + normal, color);
        handle.DrawLine(a - normal, b - normal, color);
    }

    private void DrawElement(DrawingHandleScreen handle, CadiaHoloElement e, float pulse)
    {
        var a = ToScreen(e.A);
        var b = ToScreen(e.B);
        switch (e.Shape)
        {
            case CadiaHoloShape.Line:
                Thick(handle, a, b, e.Color);
                break;
            case CadiaHoloShape.Arrow:
                Thick(handle, a, b, e.Color);
                var dir = b - a;
                if (dir.LengthSquared() > 1)
                {
                    dir = dir.Normalized();
                    var side = new Vector2(-dir.Y, dir.X);
                    var head = 10f + _scale * 0.6f;
                    Thick(handle, b, b - dir * head + side * head * 0.55f, e.Color);
                    Thick(handle, b, b - dir * head - side * head * 0.55f, e.Color);
                }
                break;
            case CadiaHoloShape.Circle:
                var r = (b - a).Length();
                handle.DrawCircle(a, r, e.Color.WithAlpha(0.12f));
                handle.DrawCircle(a, r, e.Color, false);
                handle.DrawCircle(a, r + 1, e.Color, false);
                break;
            case CadiaHoloShape.Marker:
                DrawMarker(handle, a, (CadiaHoloMarkerType)e.Kind, e.Color, pulse);
                break;
            case CadiaHoloShape.Text:
                var dims = handle.GetDimensions(_font, e.Text, 1f);
                handle.DrawRect(UIBox2.FromDimensions(a - new Vector2(3, 2), dims + new Vector2(6, 4)), Color.FromHex("#000000a0"));
                handle.DrawString(_font, a, e.Text, e.Color);
                break;
            case CadiaHoloShape.Blueprint:
                var tiles = e.Kind == (byte)CadiaBlueprintKind.Hydra ? 4 : e.Kind == (byte)CadiaBlueprintKind.Pillbox ? 3 : 1;
                var size = tiles * _scale;
                var origin = tiles > 1 ? ToScreen(e.A + new Vector2(-1.5f, tiles - 1.5f))
                    : ToScreen(e.A + new Vector2(-0.5f, 0.5f));
                var box = UIBox2.FromDimensions(origin, new Vector2(Math.Max(size, 6)));
                var color = e.Done ? Color.FromHex("#5aff78") : BlueprintColor;
                handle.DrawRect(box, color.WithAlpha(e.Done ? 0.35f : 0.18f * pulse + 0.1f));
                handle.DrawRect(box, color, false);
                if (_scale >= 6)
                {
                    var label = Loc.GetString($"cadia-blueprint-{((CadiaBlueprintKind)e.Kind).ToString().ToLowerInvariant()}");
                    handle.DrawString(_small, box.TopLeft + new Vector2(0, -13), e.Done ? "✔ " + label : label, color);
                }
                break;
        }
    }

    private void DrawMarker(DrawingHandleScreen handle, Vector2 p, CadiaHoloMarkerType type, Color color, float pulse)
    {
        const float s = 8f;
        switch (type)
        {
            case CadiaHoloMarkerType.Squad:
                handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(s), new Vector2(s * 2)), color.WithAlpha(0.25f));
                handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(s), new Vector2(s * 2)), color, false);
                handle.DrawLine(p - new Vector2(s, s), p + new Vector2(s, s), color);
                handle.DrawLine(p + new Vector2(-s, s), p + new Vector2(s, -s), color);
                break;
            case CadiaHoloMarkerType.Enemy:
                Thick(handle, p + new Vector2(0, -s - 2), p + new Vector2(s + 2, 0), color);
                Thick(handle, p + new Vector2(s + 2, 0), p + new Vector2(0, s + 2), color);
                Thick(handle, p + new Vector2(0, s + 2), p + new Vector2(-s - 2, 0), color);
                Thick(handle, p + new Vector2(-s - 2, 0), p + new Vector2(0, -s - 2), color);
                break;
            case CadiaHoloMarkerType.Target:
                handle.DrawCircle(p, s + 3 * pulse, color, false);
                handle.DrawCircle(p, 3, color);
                handle.DrawLine(p - new Vector2(s * 1.8f, 0), p - new Vector2(s * 0.6f, 0), color);
                handle.DrawLine(p + new Vector2(s * 0.6f, 0), p + new Vector2(s * 1.8f, 0), color);
                handle.DrawLine(p - new Vector2(0, s * 1.8f), p - new Vector2(0, s * 0.6f), color);
                handle.DrawLine(p + new Vector2(0, s * 0.6f), p + new Vector2(0, s * 1.8f), color);
                break;
            case CadiaHoloMarkerType.Vehicle:
                handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(s * 1.3f, s * 0.8f), new Vector2(s * 2.6f, s * 1.6f)), color, false);
                handle.DrawCircle(p, s * 0.5f, color, false);
                handle.DrawLine(p, p + new Vector2(s * 1.8f, 0), color);
                break;
            case CadiaHoloMarkerType.Landing:
                handle.DrawCircle(p, s + 2, color, false);
                handle.DrawLine(p + new Vector2(-4, -6), p + new Vector2(-4, 6), color);
                handle.DrawLine(p + new Vector2(4, -6), p + new Vector2(4, 6), color);
                handle.DrawLine(p + new Vector2(-4, 0), p + new Vector2(4, 0), color);
                break;
        }
    }

    private void DrawLive(DrawingHandleScreen handle, CadiaHoloLive live, float pulse)
    {
        var p = ToScreen(live.Position);
        switch (live.Kind)
        {
            case CadiaHoloLiveKind.Friendly:
                var squadColor = live.Squad > 0 && _state?.Squads.FirstOrDefault(s => s.Number == live.Squad) is { } squad ? squad.Color : Holo;
                handle.DrawCircle(p, live.Squad > 0 ? 3.5f : 2.5f, squadColor);
                break;
            case CadiaHoloLiveKind.Commander:
                handle.DrawCircle(p, 4f, Color.FromHex("#e8c66a"));
                handle.DrawCircle(p, 7f * pulse + 2, Color.FromHex("#e8c66a"), false);
                break;
            case CadiaHoloLiveKind.Pylon:
                handle.DrawCircle(p, 5f, Color.FromHex("#3dffc8"));
                handle.DrawCircle(p, 9f, Color.FromHex("#3dffc880"), false);
                break;
            case CadiaHoloLiveKind.Citadel:
                handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(8), new Vector2(16)), Color.FromHex("#ffd37a"), false);
                handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(4), new Vector2(8)), Color.FromHex("#ffd37a"));
                break;
            case CadiaHoloLiveKind.SectorImperium:
            case CadiaHoloLiveKind.SectorChaos:
            case CadiaHoloLiveKind.SectorNeutral:
                var sectorColor = live.Kind switch
                {
                    CadiaHoloLiveKind.SectorImperium => Color.FromHex("#e8c66a"),
                    CadiaHoloLiveKind.SectorChaos => Color.FromHex("#ff3a2a"),
                    _ => Color.FromHex("#9a9a8a"),
                };
                handle.DrawCircle(p, live.Radius * _scale, sectorColor.WithAlpha(0.08f));
                handle.DrawCircle(p, live.Radius * _scale, sectorColor.WithAlpha(0.6f), false);
                var letter = live.Label ?? string.Empty;
                handle.DrawString(_small, p - handle.GetDimensions(_small, letter, 1f) / 2f, letter, sectorColor);
                break;
            case CadiaHoloLiveKind.Beacon:
                handle.DrawLine(p + new Vector2(-7, -7), p + new Vector2(7, 7), Color.FromHex("#ffb030"));
                handle.DrawLine(p + new Vector2(-7, 7), p + new Vector2(7, -7), Color.FromHex("#ffb030"));
                handle.DrawCircle(p, 9f * pulse + 3, Color.FromHex("#ffb030"), false);
                break;
            case CadiaHoloLiveKind.Shuttle:
                handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(7, 5), new Vector2(14, 10)), Color.FromHex("#8fd8ff"), false);
                break;
            case CadiaHoloLiveKind.AntiAir:
                handle.DrawLine(p + new Vector2(0, -6), p + new Vector2(6, 4), Holo);
                handle.DrawLine(p + new Vector2(6, 4), p + new Vector2(-6, 4), Holo);
                handle.DrawLine(p + new Vector2(-6, 4), p + new Vector2(0, -6), Holo);
                break;
            case CadiaHoloLiveKind.Strike:
                handle.DrawCircle(p, 3 * _scale, Color.FromHex("#ff3a2a").WithAlpha(0.3f * pulse));
                handle.DrawCircle(p, 3 * _scale, Color.FromHex("#ff3a2a"), false);
                break;
            case CadiaHoloLiveKind.EnemyContact:
                handle.DrawLine(p + new Vector2(0, -7), p + new Vector2(7, 6), Color.FromHex("#ff3a2a"));
                handle.DrawLine(p + new Vector2(7, 6), p + new Vector2(-7, 6), Color.FromHex("#ff3a2a"));
                handle.DrawLine(p + new Vector2(-7, 6), p + new Vector2(0, -7), Color.FromHex("#ff3a2a"));
                break;
        }
    }
}
