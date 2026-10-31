using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>Пост стрелка на шаттле: башни плавно идут за сервером, курсор — прицел, ЛКМ — огонь, табло боезапаса.</summary>
public sealed class CadiaShipGunClientSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly CadiaVehicleClientSystem _vehicle = default!;

    private const string TurretLayer = "turret";
    private readonly Dictionary<EntityUid, Angle> _shown = new();
    private TimeSpan _nextAim;
    private ICursor? _cursor;
    private bool _cursorSet;
    private bool _firing;
    private CadiaShipGunHudOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, false, true), typeof(Content.Client.Interaction.DragDropSystem))
            .Register<CadiaShipGunClientSystem>();
        _hud = new CadiaShipGunHudOverlay();
        _overlays.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<CadiaShipGunClientSystem>();
        if (_hud != null)
            _overlays.RemoveOverlay(_hud);
        if (_cursorSet)
            _clyde.SetCursor(null);
    }

    private bool IsGunner()
    {
        return _player.LocalEntity is { } local && HasComp<CadiaShipGunnerComponent>(local);
    }

    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!IsGunner())
            return false;
        if (args.State == BoundKeyState.Down)
        {
            SendAim();
            RaiseNetworkEvent(new CadiaShipFireEvent(true));
            _firing = true;
        }
        else if (_firing)
        {
            RaiseNetworkEvent(new CadiaShipFireEvent(false));
            _firing = false;
        }
        return true;
    }

    private void SendAim()
    {
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == MapId.Nullspace)
            return;
        RaiseNetworkEvent(new CadiaShipAimEvent(GetNetCoordinates(_xform.ToCoordinates(mouse))));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CadiaShipGunComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gun, out var sprite, out var xform))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), TurretLayer, out _, false))
                continue;
            var shown = _shown.GetValueOrDefault(uid, gun.Angle);
            shown += new Angle(Angle.ShortestDistance(shown, gun.Angle).Theta * Math.Min(1f, frameTime * 14f));
            _shown[uid] = shown;
            _sprite.LayerSetRotation((uid, sprite), TurretLayer, shown - _xform.GetWorldRotation(xform));
        }

        if (IsGunner())
        {
            if (!_cursorSet)
            {
                _cursor ??= _vehicle.MakeCursor();
                _clyde.SetCursor(_cursor);
                _cursorSet = true;
            }
            if (_timing.RealTime >= _nextAim)
            {
                _nextAim = _timing.RealTime + TimeSpan.FromSeconds(0.1);
                SendAim();
            }
        }
        else if (_cursorSet)
        {
            _clyde.SetCursor(null);
            _cursorSet = false;
            _firing = false;
        }
    }
}

public sealed class CadiaShipGunHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaShipGunHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 13);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entMan.TryGetComponent<CadiaShipGunnerComponent>(local, out var gunner))
            return;

        var guns = 0;
        var ammo = 0;
        var gunQuery = _entMan.EntityQueryEnumerator<CadiaShipGunComponent, TransformComponent>();
        while (gunQuery.MoveNext(out var gun, out var xform))
        {
            if (xform.GridUid != gunner.Grid)
                continue;
            guns++;
            ammo += gun.Ammo;
        }
        var bays = 0;
        var bombs = 0;
        var bayQuery = _entMan.EntityQueryEnumerator<CadiaShipBombBayComponent, TransformComponent>();
        while (bayQuery.MoveNext(out var bay, out var xform))
        {
            if (xform.GridUid != gunner.Grid)
                continue;
            bays++;
            bombs += bay.Bombs;
        }

        var selected = gunner.Selected is { } gunUid && _entMan.TryGetComponent<CadiaShipGunComponent>(gunUid, out var chosen)
            ? Loc.GetString("cadia-ship-hud-selected", ("gun", _entMan.GetComponent<MetaDataComponent>(gunUid).EntityName), ("ammo", chosen.Ammo))
            : Loc.GetString("cadia-ship-hud-selected-all");

        var handle = args.ScreenHandle;
        var box = UIBox2.FromDimensions(new Vector2(args.ViewportBounds.Center.X - 200, args.ViewportBounds.Top + 148), new Vector2(400, 74));
        var amber = Color.FromHex("#ffd37a");
        handle.DrawRect(box, Color.FromHex("#0b0d0ad8"));
        handle.DrawRect(box, amber, false);
        handle.DrawString(_font, box.TopLeft + new Vector2(10, 6), Loc.GetString("cadia-ship-hud-guns", ("guns", guns), ("ammo", ammo)), amber);
        handle.DrawString(_font, box.TopLeft + new Vector2(10, 27), Loc.GetString("cadia-ship-hud-bombs", ("bays", bays), ("bombs", bombs)), amber);
        handle.DrawString(_font, box.TopLeft + new Vector2(10, 48), selected, Color.FromHex("#5aff78"));
    }
}
