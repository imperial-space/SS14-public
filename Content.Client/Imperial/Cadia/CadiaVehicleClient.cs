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
                _nextAim = _timing.RealTime + TimeSpan.FromSeconds(0.1);
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

/// <summary>Табло экипажа внизу экрана: машина, место, корпус, оружие и боезапас, перезарядка, дым, башня.</summary>
public sealed class CadiaVehicleHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color Green = Color.FromHex("#5aff78");
    private static readonly Color Amber = Color.FromHex("#ffd37a");
    private static readonly Color Red = Color.FromHex("#ff3a2a");
    private static readonly Color Panel = Color.FromHex("#0b0d0ad8");

    private readonly Font _font;
    private readonly Font _small;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaVehicleHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 13);
        _small = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entMan.TryGetComponent<CadiaVehicleCrewComponent>(local, out var crew) ||
            !_entMan.TryGetComponent<CadiaVehicleComponent>(crew.Vehicle, out var vehicle))
            return;

        var handle = args.ScreenHandle;
        // Сверху, под панелью войны: снизу табло закрывали бы руки и инвентарь. Оружие — строками под экипажем,
        // у огневой точки экипажа и дыма нет, и оружие идёт сразу под корпусом.
        var infoRows = vehicle.GunPost ? 0 : 2;
        var weaponsTop = 44 + infoRows * 18;
        var weaponRows = Math.Min(vehicle.Weapons.Count, vehicle.Ammo.Count);
        var height = weaponsTop + weaponRows * 20 + (vehicle.Firing ? 22 : 6);
        var box = UIBox2.FromDimensions(new Vector2(args.ViewportBounds.Center.X - 230, args.ViewportBounds.Top + 148), new Vector2(460, height));
        handle.DrawRect(box, Panel);
        handle.DrawRect(box, Amber, false);

        var name = _entMan.GetComponent<MetaDataComponent>(crew.Vehicle).EntityName;
        var seat = Loc.GetString(vehicle.GunPost ? "cadia-vehicle-seat-gunner" : $"cadia-vehicle-seat-{crew.Seat.ToString().ToLowerInvariant()}");
        handle.DrawString(_font, box.TopLeft + new Vector2(10, 6), $"{name} — {seat}", Amber);

        // Корпус: полоса прочности до порога разрушения.
        var integrity = 1f;
        if (_entMan.TryGetComponent<DamageableComponent>(crew.Vehicle, out _))
        {
            var damage = (float) _entMan.System<DamageableSystem>().GetTotalDamage(crew.Vehicle);
            var max = vehicle.MaxIntegrity;
            if (max > 0)
                integrity = Math.Clamp(1f - damage / max, 0f, 1f);
        }
        var bar = UIBox2.FromDimensions(box.TopLeft + new Vector2(10, 28), new Vector2(200, 10));
        handle.DrawRect(bar, Color.FromHex("#2a2a24"));
        handle.DrawRect(UIBox2.FromDimensions(bar.TopLeft, new Vector2(200 * integrity, 10)), integrity > 0.5f ? Green : integrity > 0.25f ? Amber : Red);
        handle.DrawRect(bar, Amber.WithAlpha(0.6f), false);
        handle.DrawString(_small, bar.TopRight + new Vector2(8, -3), Loc.GetString("cadia-vehicle-hud-hull", ("percent", (int) (integrity * 100))), Color.White);

        // Экипаж и дым — только у машин.
        if (!vehicle.GunPost)
        {
            handle.DrawString(_small, box.TopLeft + new Vector2(10, 44),
                Loc.GetString("cadia-vehicle-hud-crew", ("driver", vehicle.Driver != null ? "●" : "○"), ("gunner", vehicle.Gunner != null ? "●" : "○"),
                    ("passengers", vehicle.PassengerCount), ("max", vehicle.Passengers)), Color.FromHex("#b8c8b8"));
            handle.DrawString(_small, box.TopLeft + new Vector2(10, 62), Loc.GetString("cadia-vehicle-hud-smoke", ("charges", vehicle.SmokeCharges)), Color.FromHex("#b8c8b8"));
        }

        // Оружие: выбранное, боезапас, перезарядка.
        var now = _timing.CurTime;
        var x = box.Left + 10;
        for (var i = 0; i < vehicle.Weapons.Count && i < vehicle.Ammo.Count; i++)
        {
            var weapon = vehicle.Weapons[i];
            var selected = i == vehicle.SelectedWeapon;
            var y = box.Top + weaponsTop + i * 20;
            var left = vehicle.NextShot.Count > i ? vehicle.NextShot[i] - now : TimeSpan.Zero;
            // Основное орудие: снаряд в казённике и остаток в боеукладке вместо счётчика патронов.
            var ammo = weapon.Breech ? vehicle.RackCount : vehicle.Ammo[i];
            var dry = weapon.Breech ? !vehicle.Loaded : vehicle.Ammo[i] <= 0;
            var state = dry ? Loc.GetString(weapon.Breech ? "cadia-vehicle-hud-unloaded" : "cadia-vehicle-hud-empty")
                : left > TimeSpan.FromSeconds(0.3) ? Loc.GetString("cadia-vehicle-hud-reload", ("seconds", left.TotalSeconds.ToString("0.0")))
                : Loc.GetString("cadia-vehicle-hud-ready");
            var color = dry ? Red : left > TimeSpan.FromSeconds(0.3) ? Amber : Green;
            handle.DrawString(selected ? _font : _small, new Vector2(x, y), $"{(selected ? "▶ " : "  ")}{Loc.GetString(weapon.Name)}: {ammo} — {state}", selected ? color : color.WithAlpha(0.6f));
        }
        if (vehicle.Firing)
            handle.DrawString(_font, new Vector2(x, box.Bottom - 22), Loc.GetString("cadia-aa-ui-firing"), Red);
    }
}
