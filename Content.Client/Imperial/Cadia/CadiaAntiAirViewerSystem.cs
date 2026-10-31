using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Imperial.Cadia;

/// <summary>
/// Камера наводчика: пока игрок смотрит на борт через зенитку, курсор — прицел, существа на борту не видны,
/// ЛКМ по борту захватывает точку и ведёт огонь, пока кнопка зажата. Поверх обзора — прицельная сетка.
/// </summary>
public sealed class CadiaAntiAirViewerSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly HashSet<EntityUid> _hidden = new();
    private ICursor? _crosshair;
    private bool _cursorSet;
    private bool _firing;
    private CadiaAntiAirViewOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, false, true), typeof(Content.Client.Interaction.DragDropSystem))
            .Register<CadiaAntiAirViewerSystem>();

        _overlay = new CadiaAntiAirViewOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CadiaAntiAirViewerSystem>();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
        Restore();
    }

    private CadiaAntiAirViewerComponent? LocalViewer()
    {
        return _player.LocalEntity is { } local && TryComp<CadiaAntiAirViewerComponent>(local, out var viewer) && viewer.Ship != null
            ? viewer
            : null;
    }

    /// <summary>ЛКМ в мире: нажатие — захват точки и огонь, отпускание — прекратить огонь.</summary>
    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (LocalViewer() is not { Console: { } console })
            return false;

        if (args.State == BoundKeyState.Down)
        {
            _ui.ClientSendUiMessage(console, CadiaAntiAirUiKey.Key, new CadiaAntiAirLockMessage(GetNetCoordinates(args.Coordinates)));
            _ui.ClientSendUiMessage(console, CadiaAntiAirUiKey.Key, new CadiaAntiAirFireMessage(true));
            _firing = true;
        }
        else if (_firing)
        {
            _ui.ClientSendUiMessage(console, CadiaAntiAirUiKey.Key, new CadiaAntiAirFireMessage(false));
            _firing = false;
        }
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var viewer = LocalViewer();
        if (viewer == null)
        {
            Restore();
            return;
        }

        // Курсор — прицел.
        if (!_cursorSet)
        {
            _crosshair ??= MakeCrosshair();
            _clyde.SetCursor(_crosshair);
            _cursorSet = true;
        }

        // Существа на чужих картах (на борту) не видны — только корабль.
        var localMap = Transform(_player.LocalEntity!.Value).MapUid;
        var mobs = EntityQueryEnumerator<MobStateComponent, SpriteComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out _, out var sprite, out var xform))
        {
            if (xform.MapUid == localMap)
                continue;
            if (sprite.Visible)
            {
                _sprite.SetVisible((uid, sprite), false);
                _hidden.Add(uid);
            }
        }
    }

    private void Restore()
    {
        if (_cursorSet)
        {
            _clyde.SetCursor(null);
            _cursorSet = false;
        }
        _firing = false;
        if (_hidden.Count == 0)
            return;
        foreach (var uid in _hidden)
        {
            if (TryComp<SpriteComponent>(uid, out var sprite))
                _sprite.SetVisible((uid, sprite), true);
        }
        _hidden.Clear();
    }

    /// <summary>Курсор 32×32: зелёное перекрестие с кольцом и точкой, центр — горячая точка.</summary>
    private ICursor MakeCrosshair()
    {
        using var image = new Image<Rgba32>(32, 32);
        var green = new Rgba32(90, 255, 120, 255);
        var dark = new Rgba32(0, 0, 0, 200);
        void Put(int x, int y, Rgba32 c)
        {
            if (x >= 0 && y >= 0 && x < 32 && y < 32)
                image[x, y] = c;
        }
        for (var a = 0; a < 360; a += 3)
        {
            var r = a * MathF.PI / 180f;
            var x = (int) MathF.Round(15.5f + MathF.Cos(r) * 9);
            var y = (int) MathF.Round(15.5f + MathF.Sin(r) * 9);
            Put(x + 1, y + 1, dark);
            Put(x, y, green);
        }
        for (var i = 0; i < 6; i++)
        {
            foreach (var (x, y) in new[] { (15, i), (15, 31 - i), (i, 15), (31 - i, 15) })
            {
                Put(x + 1, y + 1, dark);
                Put(x, y, green);
            }
        }
        Put(15, 15, new Rgba32(255, 60, 40, 255));
        Put(16, 15, new Rgba32(255, 60, 40, 255));
        Put(15, 16, new Rgba32(255, 60, 40, 255));
        Put(16, 16, new Rgba32(255, 60, 40, 255));
        return _clyde.CreateCursor(image, new Vector2i(15, 15));
    }
}

/// <summary>
/// Прицельная сетка поверх обзора наводчика: рамка «камеры», захваченная точка на борту, курс и состояние орудия.
/// </summary>
public sealed class CadiaAntiAirViewOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color Green = Color.FromHex("#5aff78");
    private static readonly Color Red = Color.FromHex("#ff3a2a");
    private static readonly Color Amber = Color.FromHex("#ffd37a");

    private readonly Font _font;
    private readonly Font _big;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaAntiAirViewOverlay()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 12);
        _big = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 16);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entMan.TryGetComponent<CadiaAntiAirViewerComponent>(local, out var viewer) ||
            viewer.Ship is not { } ship || viewer.Gun is not { } gunUid ||
            !_entMan.TryGetComponent<CadiaAntiAirComponent>(gunUid, out var gun))
            return;

        var handle = args.ScreenHandle;
        var box = args.ViewportBounds;
        var pulse = 0.6f + 0.4f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 5f);

        // Рамка оптики: уголки, лёгкий зелёный тон и сканирующая полоса.
        handle.DrawRect(box, Green.WithAlpha(0.04f));
        const float corner = 40f;
        foreach (var (p, dx, dy) in new[] { (box.TopLeft, 1, 1), (box.TopRight, -1, 1), (box.BottomLeft, 1, -1), (box.BottomRight, -1, -1) })
        {
            var o = p + new Vector2(dx * 14, dy * 14);
            handle.DrawLine(o, o + new Vector2(dx * corner, 0), Green);
            handle.DrawLine(o, o + new Vector2(0, dy * corner), Green);
        }
        var scan = box.Top + box.Height * (float) (_timing.RealTime.TotalSeconds % 4.0 / 4.0);
        handle.DrawRect(new UIBox2(box.Left, scan - 1, box.Right, scan + 1), Green.WithAlpha(0.08f));

        // Захваченная точка на борту: ромб и пульсирующее кольцо.
        if (gun.AimGrid == ship && _entMan.TryGetComponent<TransformComponent>(ship, out var shipXform))
        {
            var xformSys = _entMan.System<SharedTransformSystem>();
            var world = Vector2.Transform(gun.AimLocal, xformSys.GetWorldMatrix(shipXform));
            var screen = _eye.MapToScreen(new Robust.Shared.Map.MapCoordinates(world, shipXform.MapID)).Position;
            var color = gun.OutOfRange ? Amber : Red;
            handle.DrawCircle(screen, 18 + 4 * pulse, color.WithAlpha(0.5f), false);
            handle.DrawLine(screen + new Vector2(0, -10), screen + new Vector2(10, 0), color);
            handle.DrawLine(screen + new Vector2(10, 0), screen + new Vector2(0, 10), color);
            handle.DrawLine(screen + new Vector2(0, 10), screen + new Vector2(-10, 0), color);
            handle.DrawLine(screen + new Vector2(-10, 0), screen + new Vector2(0, -10), color);
        }

        // Табло: борт, состояние орудия, лента, подсказка.
        var name = _entMan.GetComponentOrNull<MetaDataComponent>(ship)?.EntityName ?? string.Empty;
        var aligned = Math.Abs(Angle.ShortestDistance(gun.Aim, gun.TargetAim).Degrees) <= 4;
        var status = gun.AimGrid != ship ? Loc.GetString("cadia-aa-hud-no-lock")
            : gun.OutOfRange ? Loc.GetString("cadia-aa-ui-out-of-range")
            : aligned ? Loc.GetString("cadia-aa-ui-on-target")
            : Loc.GetString("cadia-aa-ui-traversing");
        var statusColor = gun.AimGrid != ship ? Amber : gun.OutOfRange ? Red : aligned ? Green : Amber;

        var x = box.Left + 24;
        var y = box.Top + 22;
        handle.DrawString(_big, new Vector2(x, y), Loc.GetString("cadia-aa-hud-title", ("ship", name)), Green);
        handle.DrawString(_font, new Vector2(x, y + 24), status, statusColor);
        handle.DrawString(_font, new Vector2(x, y + 42), Loc.GetString("cadia-aa-ui-ammo", ("ammo", gun.Ammo), ("max", gun.MaxAmmo)), Green.WithAlpha(0.85f));
        if (gun.Firing)
            handle.DrawString(_big, new Vector2(x, y + 62), Loc.GetString("cadia-aa-ui-firing"), Red.WithAlpha(pulse));

        var hint = Loc.GetString("cadia-aa-hud-hint");
        var size = handle.GetDimensions(_font, hint, 1f);
        handle.DrawString(_font, new Vector2(box.Center.X - size.X / 2f, box.Bottom - 30), hint, Green.WithAlpha(0.8f));
    }
}
