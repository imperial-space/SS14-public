using System.Linq;
using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval;
using Content.Shared.Imperial.Medieval.CartographerTable;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.CartographerTable;

[UsedImplicitly]
public sealed class MedievalCartographerTableSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private float _updateTimer;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<MedievalCartographerTableComponent>(RadarConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
        });

        SubscribeLocalEvent<MedievalCartographerTableComponent, ExaminedEvent>(OnExamine);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;

        var updateInterval = 0.1f;
        var intervalQuery = EntityQueryEnumerator<MedievalCartographerTableComponent, RadarConsoleComponent, TransformComponent>();
        if (intervalQuery.MoveNext(out _, out var tableComp, out _, out _))
            updateInterval = tableComp.UpdateInterval;

        if (_updateTimer < updateInterval)
            return;

        _updateTimer -= updateInterval;

        var query = EntityQueryEnumerator<MedievalCartographerTableComponent, RadarConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var radarComp, out var xform))
        {
            if (_uiSystem.GetActors(uid, RadarConsoleUiKey.Key).Count() == 0)
                continue;

            UpdateTableInterface(uid, radarComp, xform);
        }
    }

    private void OnUiOpened(EntityUid uid, MedievalCartographerTableComponent component, BoundUIOpenedEvent args)
    {
        if (!component.OpenSoundPlayed)
        {
            component.OpenSoundPlayed = true;
            component.CloseSoundPlayed = false;
            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/menu_open.ogg"), args.Actor);
        }

        if (!TryComp(uid, out RadarConsoleComponent? radarComp))
            return;

        UpdateTableInterface(uid, radarComp, Transform(uid));
    }

    private void OnUiClosed(EntityUid uid, MedievalCartographerTableComponent component, BoundUIClosedEvent args)
    {
        if (component.CloseSoundPlayed)
            return;

        component.CloseSoundPlayed = true;
        component.OpenSoundPlayed = false;
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/menu_close.ogg"), args.Actor);
    }

    private void OnExamine(EntityUid uid, MedievalCartographerTableComponent component, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var grid = Transform(uid).GridUid;

        if (grid is not { } gridUid)
            return;

        if (!TryComp<PhysicsComponent>(gridUid, out var physics))
            return;

        var messageSpeed = new FormattedMessage();
        messageSpeed.AddText(Loc.GetString("examine-carthographer-table-show-speed"));
        messageSpeed.PushColor(Color.BlueViolet);
        messageSpeed.AddText(physics.LinearVelocity.Length().ToString("F1"));
        messageSpeed.Pop();
        args.PushMessage(messageSpeed);

        var messageRotate = new FormattedMessage();
        messageRotate.AddText(Loc.GetString("examine-carthographer-table-show-rotation"));
        messageRotate.PushColor(Color.SkyBlue);
        messageRotate.AddText(Transform(gridUid).LocalRotation.Degrees.ToString("F1"));
        messageRotate.Pop();
        args.PushMessage(messageRotate);
    }

    private void UpdateTableInterface(EntityUid uid, RadarConsoleComponent radarComp, TransformComponent xform)
    {
        if (xform.MapID == MapId.Nullspace)
            return;

        var onGrid = xform.ParentUid == xform.GridUid;
        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;
        Angle? angle = onGrid ? xform.LocalRotation : null;

        if (radarComp.FollowEntity)
        {
            coordinates = new EntityCoordinates(uid, Vector2.Zero);
            angle = Angle.Zero;
        }

        if (coordinates == null || angle == null)
            return;

        var netCoordinates = GetNetCoordinates(coordinates.Value);
        var rotateWithEntity = !radarComp.FollowEntity;
        var maxRange = radarComp.MaxRange;

        var markersToSend = new List<CartographerRadarMarkerData>();
        var markerQuery = EntityQueryEnumerator<CartographerRadarMarkerComponent, TransformComponent>();

        while (markerQuery.MoveNext(out var mUid, out var mComp, out var mXform))
        {
            if (mXform.MapID != xform.MapID || mXform.MapID == MapId.Nullspace)
                continue;

            var worldPos = _transform.GetWorldPosition(mXform);

            var markerData = new CartographerRadarMarkerData
            {
                Position = worldPos,
                Color = mComp.Color,
                Size = mComp.Size,
                ZoomScaling = mComp.ZoomScaling,
                RsiPath = mComp.RsiPath,
                State = mComp.State
            };

            markersToSend.Add(markerData);
        }

        var state = new MedievalCartographerBoundUserInterfaceState(
            netCoordinates,
            angle.Value,
            maxRange,
            rotateWithEntity,
            markersToSend
        );

        _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, state);
    }
}
