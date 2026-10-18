using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Warps;
using Content.Shared.Imperial.BSA;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BSA;

public enum BSAPartKind : byte
{
    Back,
    Front,
    Middle,
}

/// <summary>Генератор, ствол или фьюзор будущей пушки (obj/machinery/bsa/back|front|middle).</summary>
[RegisterComponent]
public sealed partial class BSAPartComponent : Component
{
    [DataField(required: true)]
    public BSAPartKind Kind;

    /// <summary>Только у фьюзора: привязанный генератор.</summary>
    [ViewVariables]
    public EntityUid? Back;

    /// <summary>Только у фьюзора: привязанный ствол.</summary>
    [ViewVariables]
    public EntityUid? Front;
}

/// <summary>Буфер мультитула: последняя сохранённая часть БСА.</summary>
[RegisterComponent]
public sealed partial class BSAMultitoolBufferComponent : Component
{
    [ViewVariables]
    public EntityUid? Buffer;
}

/// <summary>Собранная пушка (obj/machinery/bsa/full).</summary>
[RegisterComponent]
public sealed partial class BSACannonComponent : Component
{
    /// <summary>Ствол смотрит на запад (иначе на восток).</summary>
    [DataField]
    public bool West = true;

    /// <summary>ex_power: взрыв у цели 3/6/12.</summary>
    [DataField]
    public float ExPower = 3f;

    /// <summary>power_used_per_shot: в SS13 «хватает, чтобы убить стандартный APC».</summary>
    [DataField]
    public float PowerPerShot = 40000f;

    /// <summary>Перезарядка (BANDASTATION ADD: BSA cooldown check).</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    /// <summary>Как далеко идёт луч (в SS13 — до края карты).</summary>
    [DataField]
    public int BeamRange = 150;

    [DataField]
    public EntProtoId Top = "ImperialBSACannonTopWest";

    [DataField]
    public EntProtoId Beam = "ImperialBSABeam";

    [DataField]
    public EntProtoId Splash = "ImperialBSASplashWest";

    [DataField]
    public EntProtoId Impact = "ImperialBSAImpact";

    [ViewVariables]
    public bool Ready;

    [ViewVariables]
    public TimeSpan ReadyAt;

    [ViewVariables]
    public EntityUid? TopEntity;

    /// <summary>Задержка между приказом и ударом (как в старой Imperial-БСА: тревога, затем выстрел).</summary>
    [DataField]
    public TimeSpan FireDelay = TimeSpan.FromSeconds(9);

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Corvax/Adminbuse/artillery.ogg");

    [ViewVariables]
    public EntityUid? PendingTarget;

    [ViewVariables]
    public EntityUid? PendingUser;

    [ViewVariables]
    public TimeSpan PendingFireAt;
}

/// <summary>Консоль управления (obj/machinery/computer/bsa_control).</summary>
[RegisterComponent]
public sealed partial class BSAConsoleComponent : Component
{
    /// <summary>Радиус поиска частей и готовой пушки (range(7)).</summary>
    [DataField]
    public float Range = 7f;

    [DataField]
    public EntProtoId CannonWest = "ImperialBSACannonWest";

    [DataField]
    public EntProtoId CannonEast = "ImperialBSACannonEast";

    [DataField]
    public ProtoId<AccessLevelPrototype> AuthAccess = "Command";

    /// <summary>Сколько ждать второго главу после первого прикладывания карты.</summary>
    [DataField]
    public TimeSpan AuthWindow = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public EntityUid? Cannon;

    [ViewVariables]
    public string? Notice;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public EntityUid? PendingCard;

    [ViewVariables]
    public EntityUid? PendingUser;

    [ViewVariables]
    public TimeSpan PendingUntil;
}

/// <summary>
/// Блюспейс-артиллерия 1 в 1 с SS13 (code/modules/station_goals/bsa.dm): три машины связываются мультитулом,
/// консоль разворачивает из них пушку 11×1, наводит её на маяк (как старая Imperial-БСА) и стреляет лучом до края карты.
/// Запуск блокирован, пока два главы не приложат ID-карты (bsa_unlock).
/// </summary>
public sealed class BSASystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private const string PulsingQuality = "Pulsing";
    private const int SpaceWidth = 10;

    /// <summary>GLOB.bsa_unlock.</summary>
    private bool _unlocked;

    private readonly HashSet<EntityUid> _tileEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _unlocked = false);

        SubscribeLocalEvent<BSAPartComponent, InteractUsingEvent>(OnPartInteractUsing);

        SubscribeLocalEvent<BSACannonComponent, MapInitEvent>(OnCannonMapInit);
        SubscribeLocalEvent<BSACannonComponent, ComponentShutdown>(OnCannonShutdown);

        SubscribeLocalEvent<BSAConsoleComponent, BoundUIOpenedEvent>(OnConsoleOpened);
        SubscribeLocalEvent<BSAConsoleComponent, BSABuildMessage>(OnBuild);
        SubscribeLocalEvent<BSAConsoleComponent, BSAFireMessage>(OnFire);
        SubscribeLocalEvent<BSAConsoleComponent, BSASetTargetMessage>(OnSetTarget);
        SubscribeLocalEvent<BSAConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);
        SubscribeLocalEvent<BSAConsoleComponent, GotEmaggedEvent>(OnConsoleEmagged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BSACannonComponent>();
        while (query.MoveNext(out var uid, out var cannon))
        {
            if (cannon.PendingTarget is { } target && now >= cannon.PendingFireAt)
            {
                var user = cannon.PendingUser ?? uid;
                cannon.PendingTarget = null;
                cannon.PendingUser = null;
                if (!TerminatingOrDeleted(target))
                    FireCannon((uid, cannon), user, _xform.GetMapCoordinates(target));
            }

            if (!cannon.Ready && cannon.PendingTarget == null && now >= cannon.ReadyAt)
                cannon.Ready = true;
        }
    }

    #region Части и мультитул

    private void OnPartInteractUsing(Entity<BSAPartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, PulsingQuality))
            return;

        args.Handled = true;
        var buffer = EnsureComp<BSAMultitoolBufferComponent>(args.Used);

        if (ent.Comp.Kind != BSAPartKind.Middle)
        {
            buffer.Buffer = ent;
            _popup.PopupEntity(Loc.GetString("bsa-multitool-saved"), ent, args.User);
            return;
        }

        if (buffer.Buffer is not { } saved || !TryComp<BSAPartComponent>(saved, out var savedPart))
            return;

        switch (savedPart.Kind)
        {
            case BSAPartKind.Back:
                ent.Comp.Back = saved;
                break;
            case BSAPartKind.Front:
                ent.Comp.Front = saved;
                break;
            default:
                return;
        }

        buffer.Buffer = null;
        _popup.PopupEntity(Loc.GetString("bsa-multitool-linked", ("part", ent.Owner), ("other", saved)), ent, args.User);
    }

    #endregion

    #region Пушка

    private void OnCannonMapInit(Entity<BSACannonComponent> ent, ref MapInitEvent args)
    {
        // Верхний слой рисуется над мобами (top_layer, ABOVE_MOB_LAYER).
        ent.Comp.TopEntity = SpawnAttachedTo(ent.Comp.Top, new EntityCoordinates(ent, Vector2.Zero));
        ent.Comp.Ready = false;
        ent.Comp.ReadyAt = _timing.CurTime + ent.Comp.Cooldown;
    }

    private void OnCannonShutdown(Entity<BSACannonComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.TopEntity is { } top)
            QueueDel(top);
    }

    private Vector2i Forward(BSACannonComponent cannon)
    {
        return cannon.West ? new Vector2i(-1, 0) : new Vector2i(1, 0);
    }

    /// <summary>get_front_turf: x∓7 от центра пушки.</summary>
    private bool TryGetFrontTile(Entity<BSACannonComponent> cannon, out EntityUid grid, out Vector2i tile)
    {
        tile = default;
        grid = default;
        var xform = Transform(cannon);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        grid = gridUid;
        tile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates) + Forward(cannon.Comp) * 7;
        return true;
    }

    /// <summary>bsa/full/fire: луч от ствола до края карты, затем взрыв у цели.</summary>
    private void FireCannon(Entity<BSACannonComponent> cannon, EntityUid user, MapCoordinates bullseye)
    {
        cannon.Comp.ReadyAt = _timing.CurTime + cannon.Comp.Cooldown;

        if (!TryGetFrontTile(cannon, out var grid, out var frontTile))
            return;

        var gridComp = Comp<MapGridComponent>(grid);
        var gridRot = _xform.GetWorldRotation(grid);
        var forward = Forward(cannon.Comp);
        var worldDir = gridRot.RotateVec(new Vector2(forward.X, forward.Y));
        var frontLocal = _map.GridTileToLocal(grid, gridComp, frontTile);
        var frontWorld = _xform.ToMapCoordinates(frontLocal);

        // Всплеск у дула (temp_visual/bsa_splash).
        var splash = Spawn(cannon.Comp.Splash, frontLocal);
        _xform.SetLocalRotation(splash, Angle.Zero);

        var beamAngle = worldDir.ToWorldAngle();
        for (var i = 1; i <= cannon.Comp.BeamRange; i++)
        {
            var position = new MapCoordinates(frontWorld.Position + worldDir * i, frontWorld.MapId);
            Devastate(position, cannon);

            var segment = Spawn(cannon.Comp.Beam, position);
            _xform.SetWorldRotation(segment, beamAngle);
        }

        _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
            $"{ToPrettyString(user):user} launched a bluespace artillery strike targeting {bullseye}");
        _chatManager.SendAdminAlert(Loc.GetString("bsa-admin-alert", ("user", ToPrettyString(user)), ("target", bullseye.ToString())));

        // explosion(bullseye, devastation = ex_power, heavy = ex_power * 2, light = ex_power * 4).
        const float slope = 5f;
        var light = cannon.Comp.ExPower * 4;
        var maxIntensity = slope * light;
        _explosion.QueueExplosion(bullseye, "Default", _explosion.RadiusToIntensity(light, slope, maxIntensity),
            slope, maxIntensity, cannon);
        Spawn(cannon.Comp.Impact, bullseye);
    }

    /// <summary>SSexplosions.highturf: всё на клетке получает полное разрушение, пол срывается в космос.</summary>
    private void Devastate(MapCoordinates position, EntityUid cannon)
    {
        if (!_mapManager.TryFindGridAt(position, out var grid, out var gridComp))
            return;

        var tile = _map.TileIndicesFor(grid, gridComp, position);
        _tileEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, tile, _tileEntities, flags: LookupFlags.Uncontained);

        foreach (var uid in _tileEntities)
        {
            if (uid == cannon || uid == grid || Transform(uid).ParentUid == cannon)
                continue;

            var damage = HasComp<MobStateComponent>(uid)
                ? new DamageSpecifier { DamageDict = { ["Heat"] = 300, ["Blunt"] = 300 } }
                : new DamageSpecifier { DamageDict = { ["Structural"] = 5000, ["Blunt"] = 1000 } };

            _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, origin: cannon);
        }

        _map.SetTile(grid, gridComp, tile, Tile.Empty);
    }

    #endregion

    #region Консоль

    private void OnConsoleOpened(Entity<BSAConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnBuild(Entity<BSAConsoleComponent> ent, ref BSABuildMessage args)
    {
        ent.Comp.Cannon = Deploy(ent);
        UpdateUi(ent);
    }

    private void OnSetTarget(Entity<BSAConsoleComponent> ent, ref BSASetTargetMessage args)
    {
        if (!_unlocked)
            return;

        var target = GetEntity(args.Target);
        if (!IsValidBeacon(ent, target))
            return;

        ent.Comp.Target = target;
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.Actor):user} has aimed the bluespace artillery strike at {ToPrettyString(target):target}");
        UpdateUi(ent);
    }

    private void OnFire(Entity<BSAConsoleComponent> ent, ref BSAFireMessage args)
    {
        if (!_unlocked)
            return;

        var user = args.Actor;
        if (ent.Comp.Cannon is not { } cannonUid || TerminatingOrDeleted(cannonUid) || !TryComp<BSACannonComponent>(cannonUid, out var cannon))
        {
            ent.Comp.Cannon = null;
            ent.Comp.Notice = Loc.GetString("bsa-notice-no-cannon");
            UpdateUi(ent);
            return;
        }

        if (!TryComp<ApcPowerReceiverComponent>(cannonUid, out var receiver) || !receiver.Powered)
        {
            ent.Comp.Notice = Loc.GetString("bsa-notice-no-power");
            UpdateUi(ent);
            return;
        }

        ent.Comp.Notice = null;

        if (!cannon.Ready)
        {
            _popup.PopupEntity(Loc.GetString("bsa-not-ready"), ent, user);
            UpdateUi(ent);
            return;
        }

        if (!TryGetImpactEntity(ent, out var impact))
        {
            UpdateUi(ent);
            return;
        }

        // cannon.use_energy(power_used_per_shot): выстрел опустошает батарею APC пушки (сколько в ней есть).
        if (receiver.Provider?.Owner is { } apc && HasComp<BatteryComponent>(apc))
            _battery.UseCharge(apc, cannon.PowerPerShot);

        cannon.Ready = false;
        cannon.PendingTarget = impact;
        cannon.PendingUser = user;
        cannon.PendingFireAt = _timing.CurTime + cannon.FireDelay;

        var beacon = ent.Comp.Target is { } selected ? TargetName(selected) : MetaData(impact).EntityName;
        _chatManager.DispatchServerAnnouncement(Loc.GetString("bsa-alert-announcement", ("beacon", beacon)), Color.Red);
        _audio.PlayGlobal(cannon.AlertSound, Filter.Broadcast(), true, AudioParams.Default.WithVolume(5f));
        UpdateUi(ent);
    }

    /// <summary>get_impact_turf: под емагом — сама консоль.</summary>
    private bool TryGetImpactEntity(Entity<BSAConsoleComponent> ent, out EntityUid impact)
    {
        impact = ent;
        if (_emag.CheckFlag(ent, EmagType.Interaction))
            return true;

        if (ent.Comp.Target is not { } target || !IsValidBeacon(ent, target))
            return false;

        impact = target;
        return true;
    }

    private void OnConsoleEmagged(Entity<BSAConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || _emag.CheckFlag(ent, EmagType.Interaction))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("bsa-emagged"), ent, args.UserUid, PopupType.MediumCaution);
    }

    /// <summary>
    /// Разблокировка запуска (keycard_auth, KEYCARD_BSA_UNLOCK): две разные карты глав подряд переключают замок.
    /// </summary>
    private void OnConsoleInteractUsing(Entity<BSAConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_idCard.TryGetIdCard(args.Used, out var card))
            return;

        args.Handled = true;
        if (!TryComp<AccessComponent>(card, out var access) || !access.Tags.Contains(ent.Comp.AuthAccess))
        {
            _popup.PopupEntity(Loc.GetString("bsa-auth-denied"), ent, args.User);
            return;
        }

        var now = _timing.CurTime;
        var pendingValid = ent.Comp.PendingCard is { } pending && now < ent.Comp.PendingUntil
            && pending != card.Owner && ent.Comp.PendingUser != args.User;

        if (!pendingValid)
        {
            ent.Comp.PendingCard = card;
            ent.Comp.PendingUser = args.User;
            ent.Comp.PendingUntil = now + ent.Comp.AuthWindow;
            _popup.PopupEntity(Loc.GetString("bsa-auth-waiting"), ent, args.User);
            return;
        }

        ent.Comp.PendingCard = null;
        ent.Comp.PendingUser = null;
        _unlocked = !_unlocked;

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(_unlocked ? "bsa-announce-unlocked" : "bsa-announce-locked"),
            Loc.GetString("bsa-announce-sender"),
            colorOverride: Color.Gold);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.User):user} {(_unlocked ? "unlocked" : "locked")} the bluespace artillery");

        var consoles = EntityQueryEnumerator<BSAConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            UpdateUi((uid, console));
        }
    }

    /// <summary>bsa_control/deploy.</summary>
    private EntityUid? Deploy(Entity<BSAConsoleComponent> ent)
    {
        var coords = Transform(ent).Coordinates;

        foreach (var cannon in _lookup.GetEntitiesInRange<BSACannonComponent>(coords, ent.Comp.Range))
        {
            return cannon;
        }

        Entity<BSAPartComponent>? centerpiece = null;
        foreach (var part in _lookup.GetEntitiesInRange<BSAPartComponent>(coords, ent.Comp.Range))
        {
            if (part.Comp.Kind == BSAPartKind.Middle)
            {
                centerpiece = part;
                break;
            }
        }

        if (centerpiece is not { } middle)
        {
            ent.Comp.Notice = Loc.GetString("bsa-notice-no-parts");
            return null;
        }

        ent.Comp.Notice = CheckCompletion(middle, out var west);
        if (ent.Comp.Notice != null)
            return null;

        var middleCoords = Transform(middle).Coordinates;
        var smoke = Spawn("Smoke", middleCoords);
        _smoke.StartSmoke(smoke, new Solution(), 4f, 4);

        var spawned = Spawn(west ? ent.Comp.CannonWest : ent.Comp.CannonEast, middleCoords);
        _xform.SetLocalRotation(spawned, Angle.Zero);
        _xform.AnchorEntity(spawned);

        if (middle.Comp.Front is { } front)
            QueueDel(front);
        if (middle.Comp.Back is { } back)
            QueueDel(back);
        QueueDel(middle);

        return spawned;
    }

    /// <summary>bsa/middle/check_completion.</summary>
    private string? CheckCompletion(Entity<BSAPartComponent> middle, out bool west)
    {
        west = true;
        if (middle.Comp.Front is not { } front || TerminatingOrDeleted(front)
            || middle.Comp.Back is not { } back || TerminatingOrDeleted(back))
        {
            return Loc.GetString("bsa-notice-not-linked");
        }

        var midXform = Transform(middle);
        var frontXform = Transform(front);
        var backXform = Transform(back);
        if (!midXform.Anchored || !frontXform.Anchored || !backXform.Anchored)
            return Loc.GetString("bsa-notice-not-anchored");

        if (midXform.GridUid is not { } grid || frontXform.GridUid != grid || backXform.GridUid != grid
            || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            return Loc.GetString("bsa-notice-misaligned");
        }

        var mid = _map.TileIndicesFor(grid, gridComp, midXform.Coordinates);
        var f = _map.TileIndicesFor(grid, gridComp, frontXform.Coordinates);
        var b = _map.TileIndicesFor(grid, gridComp, backXform.Coordinates);

        if (f.Y != mid.Y || b.Y != mid.Y)
            return Loc.GetString("bsa-notice-misaligned");

        if (f.X > mid.X && b.X < mid.X)
            west = false;
        else if (f.X < mid.X && b.X > mid.X)
            west = true;
        else
            return Loc.GetString("bsa-notice-misaligned");

        if (!HasSpace(grid, gridComp, mid, west))
            return Loc.GetString("bsa-notice-no-space");

        return null;
    }

    /// <summary>has_space: блок 10×3 без стен и космоса.</summary>
    private bool HasSpace(EntityUid grid, MapGridComponent gridComp, Vector2i mid, bool west)
    {
        var offset = west ? -6 : -4;
        var blocked = false;
        for (var x = 0; x < SpaceWidth; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var indices = new Vector2i(mid.X + offset + x, mid.Y + y);
                var tile = _map.GetTileRef(grid, gridComp, indices);
                if (!_turf.IsSpace(tile) && !HasWall(grid, gridComp, indices))
                    continue;

                blocked = true;
                Spawn("ImperialBSABlockedMarker", _map.GridTileToLocal(grid, gridComp, indices));
            }
        }

        return !blocked;
    }

    private bool HasWall(EntityUid grid, MapGridComponent gridComp, Vector2i indices)
    {
        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, indices))
        {
            if (_tag.HasTag(anchored, WallTag))
                return true;
        }

        return false;
    }

    private void UpdateUi(Entity<BSAConsoleComponent> ent)
    {
        if (ent.Comp.Cannon is { } cannonUid && TerminatingOrDeleted(cannonUid))
            ent.Comp.Cannon = null;

        string? targetName = null;
        if (ent.Comp.Target is { } target && !TerminatingOrDeleted(target))
            targetName = TargetName(target);
        else
            ent.Comp.Target = null;

        var targets = new List<(NetEntity, string)>();
        if (_unlocked)
        {
            var myMap = Transform(ent).MapID;
            var query = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
            while (query.MoveNext(out var beacon, out _, out var beaconXform))
            {
                if (beaconXform.MapID == myMap)
                    targets.Add((GetNetEntity(beacon), TargetName(beacon)));
            }

            targets.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.CurrentCulture));
        }

        var ready = ent.Comp.Cannon is { } cannon && CompOrNull<BSACannonComponent>(cannon)?.Ready == true;
        _ui.SetUiState(ent.Owner, BSAConsoleUiKey.Key,
            new BSAConsoleUiState(ent.Comp.Cannon != null, ent.Comp.Notice, _unlocked, targetName, ready, targets));
    }

    /// <summary>Маяк-цель (WarpPoint) на той же карте, что и консоль.</summary>
    private bool IsValidBeacon(EntityUid console, EntityUid target)
    {
        return !TerminatingOrDeleted(target)
            && HasComp<WarpPointComponent>(target)
            && Transform(target).MapID == Transform(console).MapID;
    }

    /// <summary>Имя маяка: его локация или название сущности.</summary>
    private string TargetName(EntityUid beacon)
    {
        return CompOrNull<WarpPointComponent>(beacon)?.Location ?? MetaData(beacon).EntityName;
    }

    #endregion
}
