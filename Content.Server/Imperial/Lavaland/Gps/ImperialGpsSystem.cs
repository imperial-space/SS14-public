
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Imperial.Lavaland.Gps;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Gps;

/// <summary>Перенос datum/component/gps и gps/item из SS13.</summary>
public sealed class ImperialGpsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    private const int MaxTagLength = 20;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialGpsDeviceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialGpsDeviceComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ImperialGpsDeviceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialGpsDeviceComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<ImperialGpsDeviceComponent, EmpPulseEvent>(OnEmp);

        Subs.BuiEvents<ImperialGpsDeviceComponent>(ImperialGpsUiKey.Key, subs =>
        {
            subs.Event<ImperialGpsPowerMessage>((uid, comp, msg) => ToggleTracking((uid, comp), msg.Actor));
            subs.Event<ImperialGpsUpdatingMessage>((uid, comp, _) =>
            {
                comp.Updating = !comp.Updating;
                UpdateUi((uid, comp));
            });
            subs.Event<ImperialGpsGlobalModeMessage>((uid, comp, _) =>
            {
                comp.GlobalMode = !comp.GlobalMode;
                UpdateUi((uid, comp));
            });
            subs.Event<ImperialGpsRefreshMessage>((uid, comp, _) => UpdateUi((uid, comp)));
            subs.Event<ImperialGpsRenameMessage>(OnRename);
        });
    }

    private void OnMapInit(Entity<ImperialGpsDeviceComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.BaseName = MetaData(ent).EntityName;
        var gps = EnsureComp<ImperialGpsComponent>(ent);
        UpdateName(ent, gps);
        UpdateOverlay(ent, gps);
    }

    private void UpdateName(Entity<ImperialGpsDeviceComponent> ent, ImperialGpsComponent gps)
    {
        _meta.SetEntityName(ent, $"{ent.Comp.BaseName} ({gps.Tag})");
    }

    private void UpdateOverlay(EntityUid uid, ImperialGpsComponent gps)
    {
        _appearance.SetData(uid, MegafaunaVisuals.Overlay, gps.Emped ? "emp" : gps.Tracking ? "working" : string.Empty);
    }

    private void OnExamined(Entity<ImperialGpsDeviceComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<ImperialGpsComponent>(ent, out var gps))
            args.PushMarkup(Loc.GetString(gps.Tracking ? "imperial-gps-examine-off" : "imperial-gps-examine-on"));
    }

    private void OnAltVerbs(Entity<ImperialGpsDeviceComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-gps-verb-toggle"),
            Act = () => ToggleTracking(ent, user),
        });
    }

    /// <summary>toggletracking.</summary>
    private void ToggleTracking(Entity<ImperialGpsDeviceComponent> ent, EntityUid user)
    {
        if (!TryComp<ImperialGpsComponent>(ent, out var gps))
            return;

        if (gps.Emped)
        {
            _popup.PopupEntity(Loc.GetString("imperial-gps-busted"), ent, user);
            return;
        }

        gps.Tracking = !gps.Tracking;
        _popup.PopupEntity(Loc.GetString(gps.Tracking ? "imperial-gps-tracking-on" : "imperial-gps-tracking-off", ("gps", ent.Owner)), ent, user);
        UpdateOverlay(ent, gps);
        UpdateUi(ent);
    }

    private void OnRename(Entity<ImperialGpsDeviceComponent> ent, ref ImperialGpsRenameMessage args)
    {
        var tag = args.Tag.Trim();
        if (tag.Length == 0 || !TryComp<ImperialGpsComponent>(ent, out var gps))
            return;
        if (tag.Length > MaxTagLength)
            tag = tag[..MaxTagLength];

        gps.Tag = tag;
        UpdateName(ent, gps);
        UpdateUi(ent);
    }

    /// <summary>on_emp_act: выключается на 30 секунд.</summary>
    private void OnEmp(Entity<ImperialGpsDeviceComponent> ent, ref EmpPulseEvent args)
    {
        if (!TryComp<ImperialGpsComponent>(ent, out var gps))
            return;

        args.Affected = true;
        gps.Emped = true;
        ent.Comp.EmpEnd = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.EmpDuration);
        UpdateOverlay(ent, gps);
        _ui.CloseUi(ent.Owner, ImperialGpsUiKey.Key);
    }

    private void OnUiOpened(Entity<ImperialGpsDeviceComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (TryComp<ImperialGpsComponent>(ent, out var gps) && gps.Emped)
        {
            _popup.PopupEntity(Loc.GetString("imperial-gps-fizzles", ("gps", ent.Owner)), ent, args.Actor);
            _ui.CloseUi(ent.Owner, ImperialGpsUiKey.Key);
            return;
        }

        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<ImperialGpsDeviceComponent, ImperialGpsComponent>();
        while (query.MoveNext(out var uid, out var device, out var gps))
        {
            if (gps.Emped && now >= device.EmpEnd)
            {
                gps.Emped = false;
                UpdateOverlay(uid, gps);
            }

            if (!device.Updating || now < device.NextUpdate)
                continue;
            device.NextUpdate = now + TimeSpan.FromSeconds(device.UpdateInterval);

            if (_ui.IsUiOpen(uid, ImperialGpsUiKey.Key))
                UpdateUi((uid, device));
        }
    }

    /// <summary>Тег сигнала с учётом локализации.</summary>
    public string GetTag(ImperialGpsComponent gps)
    {
        return gps.TagLoc is { } loc ? Loc.GetString(loc) : gps.Tag;
    }

    /// <summary>Координаты x, y, z (z — номер карты).</summary>
    private (int X, int Y, int Z) Coords(EntityUid uid)
    {
        var map = _transform.GetMapCoordinates(uid);
        return ((int) MathF.Floor(map.Position.X), (int) MathF.Floor(map.Position.Y), (int) map.MapId);
    }

    /// <summary>ui_data.</summary>
    private void UpdateUi(Entity<ImperialGpsDeviceComponent> ent)
    {
        if (!TryComp<ImperialGpsComponent>(ent, out var self))
            return;

        var state = new ImperialGpsUiState
        {
            Power = self.Tracking,
            Tag = self.Tag,
            Updating = ent.Comp.Updating,
            GlobalMode = ent.Comp.GlobalMode,
            Emped = self.Emped,
        };

        if (self.Tracking && !self.Emped)
        {
            var xform = Transform(ent);
            var area = xform.GridUid is { } grid && !HasComp<MapComponent>(grid)
                ? MetaData(grid).EntityName
                : xform.MapUid is { } mapUid ? MetaData(mapUid).EntityName : "???";
            var cur = Coords(ent);
            state.CurrentArea = area;
            state.CurrentCoords = $"{cur.X}, {cur.Y}, {cur.Z}";

            var query = EntityQueryEnumerator<ImperialGpsComponent>();
            while (query.MoveNext(out var uid, out var gps))
            {
                if (uid == ent.Owner || gps.Emped || !gps.Tracking || TerminatingOrDeleted(uid))
                    continue;

                var pos = Coords(uid);
                if (!ent.Comp.GlobalMode && pos.Z != cur.Z)
                    continue;

                var signal = new ImperialGpsSignal
                {
                    Tag = GetTag(gps),
                    Coords = $"{pos.X}, {pos.Y}, {pos.Z}",
                };

                if (pos.Z == cur.Z)
                {
                    var dx = pos.X - cur.X;
                    var dy = pos.Y - cur.Y;
                    signal.Distance = (int) MathF.Round(MathF.Sqrt(dx * dx + dy * dy));
                    // get_angle: по часовой от севера
                    var deg = MathF.Atan2(dx, dy) * 180f / MathF.PI;
                    signal.Degrees = (int) MathF.Round(deg < 0 ? deg + 360 : deg);
                }

                state.Signals.Add(signal);
            }
        }

        _ui.SetUiState(ent.Owner, ImperialGpsUiKey.Key, state);
    }
}
