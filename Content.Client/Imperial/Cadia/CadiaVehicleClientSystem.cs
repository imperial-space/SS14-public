using System.Numerics;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
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
/// Техника на клиенте: башня плавно крутится за сервером; наводчик ведёт её курсором (прицел — перекрестие)
/// и стреляет ЛКМ; экипаж видит табло машины — корпус, оружие, боезапас, перезарядку, дым.
/// </summary>
public sealed class CadiaVehicleClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const string TurretLayer = "turret";
    private const float PostGunnerBack = 0.6f;
    private readonly HashSet<EntityUid> _postGunners = new();
    private readonly HashSet<EntityUid> _postGunnersNow = new();
    private readonly Dictionary<EntityUid, Angle> _shown = new();
    private TimeSpan _nextAim;
    private ICursor? _cursor;
    private bool _cursorSet;
    private bool _firing;
    private CadiaVehicleHudOverlay? _hud;

    private static readonly TimeSpan AimDelay = TimeSpan.FromSeconds(0.1);

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, false, true), typeof(Content.Client.Interaction.DragDropSystem))
            .Register<CadiaVehicleClientSystem>();
        _hud = new CadiaVehicleHudOverlay();
        _overlays.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CadiaVehicleClientSystem>();
        if (_hud != null)
            _overlays.RemoveOverlay(_hud);
        if (_cursorSet)
            _clyde.SetCursor(null);
    }

    /// <summary>Машина, где локальный игрок — стрелок.</summary>
    private Entity<CadiaVehicleComponent>? GunnerVehicle()
    {
        if (_player.LocalEntity is not { } local || !TryComp<CadiaVehicleCrewComponent>(local, out var crew) ||
            !TryComp<CadiaVehicleComponent>(crew.Vehicle, out var vehicle))
            return null;
        var gunner = crew.Seat == CadiaVehicleSeat.Gunner || (crew.Seat == CadiaVehicleSeat.Driver && vehicle.DriverIsGunner);
        return gunner ? (crew.Vehicle, vehicle) : null;
    }

    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (GunnerVehicle() is not { } vehicle)
            return false;
        if (args.State == BoundKeyState.Down)
        {
            SendAim(vehicle);
            RaiseNetworkEvent(new CadiaVehicleFireEvent(GetNetEntity(vehicle), true));
            _firing = true;
        }
        else if (_firing)
        {
            RaiseNetworkEvent(new CadiaVehicleFireEvent(GetNetEntity(vehicle), false));
            _firing = false;
        }
        return true;
    }

    private void SendAim(EntityUid vehicle)
    {
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == Robust.Shared.Map.MapId.Nullspace)
            return;
        var coords = _xform.ToCoordinates(mouse);
        RaiseNetworkEvent(new CadiaVehicleAimEvent(GetNetEntity(vehicle), GetNetCoordinates(coords)));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Башни всех машин: угол башни в мире минус поворот корпуса, догоняем сервер плавно.
        var query = EntityQueryEnumerator<CadiaVehicleComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var vehicle, out var sprite, out var xform))
        {
            if (!vehicle.HasTurret || !_sprite.LayerMapTryGet((uid, sprite), TurretLayer, out _, false))
                continue;
            var shown = _shown.GetValueOrDefault(uid, vehicle.TurretAngle);
            shown += new Angle(Angle.ShortestDistance(shown, vehicle.TurretAngle).Theta * Math.Min(1f, frameTime * 12f));
            _shown[uid] = shown;
            // Спрайт с noRot не поворачивается вместе с корпусом (огневые точки) — и вычитать поворот корпуса не нужно,
            // иначе ствол гнезда, поставленного боком, смотрит мимо прицела ровно на угол установки.
            var hull = sprite.NoRotation ? Angle.Zero : _xform.GetWorldRotation(xform);
            _sprite.LayerSetRotation((uid, sprite), TurretLayer, shown - hull);
        }

        UpdatePostGunners();

        var gunner = GunnerVehicle();
        if (gunner is { } v)
        {
            if (!_cursorSet)
            {
                _cursor ??= MakeCursor();
                _clyde.SetCursor(_cursor);
                _cursorSet = true;
            }
            if (_timing.RealTime >= _nextAim)
            {
                _nextAim = _timing.RealTime + AimDelay;
                SendAim(v);
            }
        }
        else if (_cursorSet)
        {
            _clyde.SetCursor(null);
            _cursorSet = false;
            _firing = false;
        }
    }

    /// <summary>Стрелок открытой позиции рисуется за стволом; сервер держит его в точке ремня.</summary>
    private void UpdatePostGunners()
    {
        _postGunnersNow.Clear();
        var posts = EntityQueryEnumerator<CadiaVehicleComponent>();
        while (posts.MoveNext(out var uid, out var vehicle))
        {
            if (!vehicle.GunPost || vehicle.Driver is not { } gunner || !TryComp<SpriteComponent>(gunner, out var sprite))
                continue;
            var angle = _shown.GetValueOrDefault(uid, vehicle.TurretAngle);
            // Смещение спрайта крутится вместе с мобом (он развёрнут по стволу), поэтому точку за стволом в мире
            // переводим в систему спрайта: иначе при выстреле вбок стрелок уезжает к дулу.
            var world = angle.RotateVec(new Vector2(0, -PostGunnerBack));
            var spriteRot = sprite.NoRotation ? -_eye.CurrentEye.Rotation : _xform.GetWorldRotation(gunner);
            _sprite.SetOffset((gunner, sprite), (-spriteRot).RotateVec(world));
            _postGunnersNow.Add(gunner);
        }

        foreach (var old in _postGunners)
        {
            if (!_postGunnersNow.Contains(old) && TryComp<SpriteComponent>(old, out var sprite))
                _sprite.SetOffset((old, sprite), Vector2.Zero);
        }
        _postGunners.Clear();
        _postGunners.UnionWith(_postGunnersNow);
    }

    public ICursor MakeCursor()
    {
        using var image = new Image<Rgba32>(32, 32);
        var c = new Rgba32(255, 210, 90, 255);
        var d = new Rgba32(0, 0, 0, 200);
        void Put(int x, int y, Rgba32 col)
        {
            if (x >= 0 && y >= 0 && x < 32 && y < 32)
                image[x, y] = col;
        }
        // Угловые скобки и точка — танковый прицел.
        foreach (var (sx, sy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
        {
            for (var i = 0; i < 6; i++)
            {
                Put(15 + sx * (12 - i), 15 + sy * 12 + 1, d);
                Put(15 + sx * (12 - i), 15 + sy * 12, c);
                Put(15 + sx * 12 + 1, 15 + sy * (12 - i), d);
                Put(15 + sx * 12, 15 + sy * (12 - i), c);
            }
        }
        for (var i = 0; i < 3; i++)
        {
            Put(15, 13 - i, c);
            Put(15, 17 + i, c);
            Put(13 - i, 15, c);
            Put(17 + i, 15, c);
        }
        Put(15, 15, new Rgba32(255, 60, 40, 255));
        return _clyde.CreateCursor(image, new Vector2i(15, 15));
    }
}
