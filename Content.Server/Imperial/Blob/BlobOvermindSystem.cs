using System.Linq;
using Content.Server.Actions;
using Content.Server.AlertLevel;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Hands.Systems;
using Content.Server.Imperial.Blob.Components;
using Content.Server.Mind;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Blob;

/// <summary>
/// Оверманд блоба (mob/eye/blob, powers.dm, _onclick/overmind.dm из SS13).
/// </summary>
public sealed class BlobOvermindSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly BlobStructureSystem _structure = default!;
    [Dependency] private readonly BlobStrainSystem _strain = default!;
    [Dependency] private readonly BlobMobSystem _blobMob = default!;
    [Dependency] private readonly BlobRuleSystem _rule = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    public const string BlobFaction = "Blob";
    public const string RallyKey = "BlobRallyPoint";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobOvermindComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BlobOvermindComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlobOvermindComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<BlobOvermindComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BlobOvermindComponent, GetVisMaskEvent>(OnGetVisMask);
        SubscribeLocalEvent<BlobOvermindComponent, TransformSpeechEvent>(OnOvermindSpeech);
        SubscribeLocalEvent<BlobMobComponent, TransformSpeechEvent>(OnMinionSpeech);

        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(HandleClick), typeof(SharedInteractionSystem))
            .BindBefore(ContentKeyFunctions.TryPullObject, new PointerInputCmdHandler(HandleCtrlClick), typeof(SharedInteractionSystem))
            .BindBefore(ContentKeyFunctions.AltActivateItemInWorld, new PointerInputCmdHandler(HandleAltClick), typeof(SharedInteractionSystem))
            .Bind(ContentKeyFunctions.SaveItemLocation, new PointerInputCmdHandler(HandleMiddleClick))
            .Register<BlobOvermindSystem>();

        SubscribeLocalEvent<BlobOvermindComponent, BlobJumpToCoreActionEvent>(OnJumpToCore);
        SubscribeLocalEvent<BlobOvermindComponent, BlobJumpToNodeActionEvent>(OnJumpToNode);
        SubscribeLocalEvent<BlobOvermindComponent, BlobCreateResourceActionEvent>(OnCreateResource);
        SubscribeLocalEvent<BlobOvermindComponent, BlobCreateNodeActionEvent>(OnCreateNode);
        SubscribeLocalEvent<BlobOvermindComponent, BlobCreateFactoryActionEvent>(OnCreateFactory);
        SubscribeLocalEvent<BlobOvermindComponent, BlobCreateBlobbernautActionEvent>(OnCreateBlobbernaut);
        SubscribeLocalEvent<BlobOvermindComponent, BlobReadaptStrainActionEvent>(OnReadaptStrain);
        SubscribeLocalEvent<BlobOvermindComponent, BlobRelocateCoreActionEvent>(OnRelocateCore);

        SubscribeLocalEvent<BlobOvermindComponent, BlobSelectStrainMessage>(OnSelectStrain);
        SubscribeLocalEvent<BlobOvermindComponent, BlobJumpToNodeMessage>(OnJumpToNodeMessage);
    }

    #region Создание и уничтожение

    private void OnMapInit(Entity<BlobOvermindComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        var comp = ent.Comp;
        comp.Points = comp.StartingPoints;
        comp.ManualPlaceTime = now + comp.ManualPlaceDelay;
        comp.AutoPlaceTime = now + comp.AutoPlaceDelay;
        comp.LastAttack = now;
        comp.LastReroll = now;
        comp.NextProcess = now + TimeSpan.FromSeconds(1);

        var name = Loc.GetString("blob-overmind-name", ("number", _random.Next(1, 1000)));
        _meta.SetEntityName(ent, name);

        // var/datum/blobstrain/BS = pick(GLOB.valid_blobstrains)
        var strains = _proto.EnumeratePrototypes<BlobStrainPrototype>().Where(s => s.Selectable).ToList();
        if (strains.Count > 0)
            comp.Strain = _random.Pick(strains).ID;
        UpdateOvermindColor(ent);

        foreach (var action in comp.ActionPrototypes)
        {
            if (_actions.AddAction(ent, action) is { } actionUid)
                comp.Actions.Add(actionUid);
        }
        UpdateCoreButton(ent);


        // INVISIBILITY_OBSERVER: экипаж не видит камеру оверманда.
        var visibility = EnsureComp<VisibilityComponent>(ent);
        _visibility.AddLayer((ent, visibility), (int) VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer((ent, visibility), (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(ent, visibilityComponent: visibility);
        _eye.RefreshVisibilityMask(ent.Owner);
        _faction.AddFaction(ent.Owner, BlobFaction);
        comp.LastValidPosition = Transform(ent).Coordinates;
        Dirty(ent);
    }

    private void OnGetVisMask(Entity<BlobOvermindComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Ghost;
    }

    private void OnShutdown(Entity<BlobOvermindComponent> ent, ref ComponentShutdown args)
    {
        _rule.RecordResult(ent);

        // cleanup_overmind_blobs: структуры становятся мёртвыми.
        foreach (var blob in ent.Comp.AllBlobs.ToList())
        {
            if (TryComp<BlobStructureComponent>(blob, out var comp) && comp.Overmind == ent.Owner)
                _structure.ClearOvermind((blob, comp));
        }

        foreach (var mob in ent.Comp.Mobs.ToList())
            _blobMob.OnOvermindDeleted(mob, ent);

        ent.Comp.AllBlobs.Clear();
        ent.Comp.Mobs.Clear();
    }

    private void OnMindAdded(Entity<BlobOvermindComponent> ent, ref MindAddedMessage args)
    {
        _rule.EnsureBlobAntag(args.Mind, ent.Comp);

        // stinger_sound: оверманд из призраков ещё не слышал сигнал роли.
        if (ent.Comp.Rule == null)
            _audio.PlayGlobal(ent.Comp.AlertSound, Filter.Entities(ent.Owner), true);

        var strain = GetStrain(ent.Comp);
        SendMessage(ent, Loc.GetString("blob-overmind-login"));
        SendMessage(ent, Loc.GetString("blob-overmind-help"));
        if (!ent.Comp.Placed)
        {
            SendMessage(ent, Loc.GetString("blob-overmind-autoplace",
                ("time", FormatTime(ent.Comp.AutoPlaceTime - _timing.CurTime))));
            SendMessage(ent, Loc.GetString(ent.Comp.ManualPlaceTime > _timing.CurTime ? "blob-overmind-manual-later" : "blob-overmind-manual-now"));
        }

        if (strain != null)
            SendStrainInfo(ent, strain);
    }

    private void OnExamined(Entity<BlobOvermindComponent> ent, ref ExaminedEvent args)
    {
        if (GetStrain(ent.Comp) is { } strain)
            args.PushMarkup(Loc.GetString("blob-overmind-examine-strain", ("color", strain.Color.ToHex()), ("name", Loc.GetString(strain.Name))));
    }

    #endregion

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<BlobOvermindSystem>();
    }

    #region Процесс

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BlobOvermindComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            ClampCamera(uid, comp);

            if (comp.VictoryTime is { } victoryTime && now >= victoryTime)
            {
                comp.VictoryTime = null;
                Victory(uid, comp);
            }

            if (now < comp.NextProcess)
                continue;
            comp.NextProcess = now + TimeSpan.FromSeconds(1);
            Process(uid, comp, now);
        }
    }

    /// <summary>mob/eye/blob/process().</summary>
    private void Process(EntityUid uid, BlobOvermindComponent comp, TimeSpan now)
    {
        if (comp.Core == null || TerminatingOrDeleted(comp.Core.Value))
        {
            if (!comp.Placed)
            {
                if (!comp.ManualPlaceNotified && now >= comp.ManualPlaceTime)
                {
                    comp.ManualPlaceNotified = true;
                    SendMessage(uid, Loc.GetString("blob-overmind-can-place"));
                    SendMessage(uid, Loc.GetString("blob-overmind-autoplace", ("time", FormatTime(comp.AutoPlaceTime - now))));
                }

                if (now >= comp.AutoPlaceTime)
                    PlaceCore(uid, comp, BlobPlacement.Random);
            }
            else
            {
                // Блоб убит.
                QueueDel(uid);
            }

            return;
        }

        if (!comp.VictoryInProgress && comp.BlobsLegit.Count >= comp.WinCount)
        {
            BeginVictory(uid, comp);
        }
        else if (comp.FreeRerolls <= 0 && comp.LastReroll + comp.RerollFreeTime < now)
        {
            SendMessage(uid, Loc.GetString("blob-overmind-free-reroll"));
            comp.FreeRerolls = 1;
            Dirty(uid, comp);
        }

        if (!comp.VictoryInProgress && comp.MaxCount < comp.BlobsLegit.Count)
            comp.MaxCount = comp.BlobsLegit.Count;

        if (comp.AnnouncementTime is { } announce && !comp.HasAnnounced &&
            (now >= announce || comp.BlobsLegit.Count >= comp.AnnouncementSize))
        {
            comp.HasAnnounced = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("blob-announcement-outbreak"),
                Loc.GetString("blob-announcement-outbreak-sender"),
                announcementSound: comp.OutbreakSound, colorOverride: Color.FromHex("#d94b4b"));
        }
    }

    /// <summary>Move(): камера не уходит дальше 1 тайла от блоба; до размещения — не в космос и не на шаттлы.</summary>
    private void ClampCamera(EntityUid uid, BlobOvermindComponent comp)
    {
        var xform = Transform(uid);
        var valid = false;
        if (_structure.TryGetTile(uid, out var grid, out var tile))
        {
            if (comp.Placed)
                valid = _structure.BlobsInRange(grid, tile, comp.MaxCameraStray).Count > 0;
            else
                valid = _structure.IsStationGrid(grid) && _map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) && !tileRef.Tile.IsEmpty;
        }

        if (valid)
        {
            comp.LastValidPosition = xform.Coordinates;
            return;
        }

        if (comp.LastValidPosition is { } last && last.IsValid(EntityManager))
            _transform.SetCoordinates(uid, last);
        else if (comp.Core is { } core && !TerminatingOrDeleted(core))
            _transform.SetCoordinates(uid, Transform(core).Coordinates);
    }

    /// <summary>begin_victory(): критическая масса.</summary>
    private void BeginVictory(EntityUid uid, BlobOvermindComponent comp)
    {
        comp.VictoryInProgress = true;
        _chat.DispatchGlobalAnnouncement(Loc.GetString("blob-announcement-critical"),
            Loc.GetString("blob-announcement-critical-sender"), colorOverride: Color.FromHex("#d94b4b"));

        if (comp.Core is { } core && _station.GetOwningStation(core) is { } station)
            _alertLevel.SetLevel(station, "delta", true, true, true);

        if (comp.EndRoundOnVictory)
        {
            comp.MaxPoints = float.PositiveInfinity;
            comp.Points = float.PositiveInfinity;
        }
        else
        {
            SendMessage(uid, Loc.GetString("blob-overmind-critical-no-end"));
        }

        comp.VictoryTime = _timing.CurTime + comp.VictoryDelay;
        Dirty(uid, comp);
    }

    /// <summary>victory(): победа блоба.</summary>
    private void Victory(EntityUid uid, BlobOvermindComponent comp)
    {
        comp.Victorious = true;

        if (!comp.EndRoundOnVictory)
        {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("blob-announcement-countermeasures"),
                Loc.GetString("blob-announcement-countermeasures-sender"), colorOverride: Color.FromHex("#d94b4b"));
            return;
        }

        _chatManager.DispatchServerAnnouncement(Loc.GetString("blob-victory-world", ("name", Name(uid))), Color.FromHex("#4aa34a"));
        _audio.PlayGlobal(comp.NukeAlarmSound, Filter.Broadcast(), true, AudioParams.Default.WithVolume(BlobStructureSystem.Db(70)));
        Timer.Spawn(TimeSpan.FromSeconds(10), () => VictorySequence(uid));
    }

    /// <summary>victory_sequence(): все не-блобы на станции погибают, на их месте появляются споры.</summary>
    private void VictorySequence(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !TryComp<BlobOvermindComponent>(uid, out var comp))
            return;

        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        var victims = new List<(EntityUid, EntityCoordinates)>();
        var allies = new List<EntityUid>();
        while (query.MoveNext(out var mob, out _, out var xform))
        {
            if (xform.GridUid is not { } grid || !_structure.IsStationGrid(grid))
                continue;
            if (IsBlobAlly(mob))
                allies.Add(mob);
            else
                victims.Add((mob, xform.Coordinates));
        }

        foreach (var (mob, coords) in victims)
        {
            _audio.PlayPvs(comp.SplatSound, coords, AudioParams.Default.WithVolume(BlobStructureSystem.Db(50)).WithVariation(0.125f));
            _mobState.ChangeMobState(mob, MobState.Dead);
            _blobMob.CreateSpore(uid, coords, BlobMobType.Spore);
        }

        foreach (var ally in allies)
            _blobMob.FullyHeal(ally);

        _gameTicker.EndRound(Loc.GetString("blob-round-end-victory", ("count", comp.BlobsLegit.Count)));
    }

    #endregion

    #region Ресурсы и HUD

    public BlobStrainPrototype? GetStrain(BlobOvermindComponent comp) =>
        _proto.TryIndex(comp.Strain, out var strain) ? strain : null;

    /// <summary>add_points().</summary>
    public void AddPoints(EntityUid uid, BlobOvermindComponent comp, float points)
    {
        comp.Points = Math.Clamp(comp.Points + points, 0, comp.MaxPoints);
        Dirty(uid, comp);
    }

    /// <summary>can_buy(): списывает цену, если хватает ресурсов.</summary>
    public bool CanBuy(EntityUid uid, BlobOvermindComponent comp, float cost)
    {
        if (comp.Points < cost)
        {
            SendMessage(uid, Loc.GetString("blob-cannot-afford", ("cost", cost)));
            _popup.PopupEntity(Loc.GetString("blob-need-more", ("amount", MathF.Ceiling(cost - comp.Points))), uid, uid);
            return false;
        }

        AddPoints(uid, comp, -cost);
        return true;
    }

    /// <summary>update_health_hud(): здоровье ядра для оверманда и блоббернаутов.</summary>
    public void UpdateHud(EntityUid uid, BlobOvermindComponent comp)
    {
        var health = -1;
        if (comp.Core is { } core && TryComp<BlobStructureComponent>(core, out var coreComp))
            health = (int) MathF.Round(coreComp.Integrity / coreComp.MaxIntegrity * 100);

        if (comp.CoreHealth != health)
        {
            comp.CoreHealth = health;
            Dirty(uid, comp);
        }

        foreach (var mob in comp.Mobs)
        {
            if (!TryComp<BlobMobComponent>(mob, out var mobComp) || mobComp.Type != BlobMobType.Blobbernaut ||
                mobComp.OvermindCoreHealth == health)
                continue;
            mobComp.OvermindCoreHealth = health;
            Dirty(mob, mobComp);
        }
    }

    public bool IsBlobAlly(EntityUid uid) =>
        HasComp<BlobMobComponent>(uid) || HasComp<BlobOvermindComponent>(uid) || _faction.IsMember(uid, BlobFaction);

    private void UpdateOvermindColor(Entity<BlobOvermindComponent> ent)
    {
        if (GetStrain(ent.Comp) is { } strain)
            _appearance.SetData(ent, BlobVisuals.Color, strain.ComplementaryColor);
    }

    #endregion

    #region Размещение ядра

    public enum BlobPlacement : byte
    {
        Normal,
        Random,
        Force,
    }

    /// <summary>place_blob_core().</summary>
    public bool PlaceCore(EntityUid uid, BlobOvermindComponent comp, BlobPlacement placement, bool popOverride = false)
    {
        if (comp.Placed && placement != BlobPlacement.Force)
            return false;

        if (placement == BlobPlacement.Normal)
        {
            if (!popOverride && !CheckCoreVisibility(uid))
                return false;

            if (!_structure.TryGetTile(uid, out var grid, out var tile) || !IsValidTile(grid, tile))
            {
                SendMessage(uid, Loc.GetString("blob-core-invalid-spot"));
                return false;
            }

            if (!CheckObjectsTile(uid, grid, tile))
                return false;

            var now = _timing.CurTime;
            if (!popOverride && now <= comp.ManualPlaceTime && now <= comp.AutoPlaceTime)
            {
                SendMessage(uid, Loc.GetString("blob-core-too-early"));
                return false;
            }
        }
        else if (placement == BlobPlacement.Random)
        {
            if (FindRandomBlobStart() is { } start)
                _transform.SetCoordinates(uid, start);
        }

        if (!_structure.TryGetTile(uid, out var placeGrid, out var placeTile))
            return false;

        if (comp.Placed && comp.Core is { } existing && !TerminatingOrDeleted(existing))
        {
            _structure.MoveBlob(existing, placeGrid, placeTile);
        }
        else
        {
            // Обычный блоб на месте ядра поглощается.
            if (_structure.GetBlobAt(placeGrid, placeTile) is { } old)
                QueueDel(old);

            var core = _structure.SpawnBlob(comp.CorePrototype, placeGrid, placeTile, uid);
            comp.Core = core;
            _transform.SetCoordinates(uid, Transform(core).Coordinates);
        }

        comp.Placed = true;
        comp.AnnouncementTime = _timing.CurTime + comp.AnnouncementDelay;
        UpdateCoreButton((uid, comp));
        comp.LastValidPosition = Transform(uid).Coordinates;
        UpdateHud(uid, comp);
        Dirty(uid, comp);
        return true;
    }

    /// <summary>jump_to_core/MouseEntered: до размещения кнопка называется «Place Blob Core».</summary>
    private void UpdateCoreButton(Entity<BlobOvermindComponent> ent)
    {
        foreach (var action in ent.Comp.Actions)
        {
            if (MetaData(action).EntityPrototype?.ID != "ActionBlobJumpToCore")
                continue;
            _meta.SetEntityName(action, Loc.GetString(ent.Comp.Placed ? "blob-button-jump-core" : "blob-button-place-core"));
            _meta.SetEntityDescription(action, Loc.GetString(ent.Comp.Placed ? "blob-button-jump-core-desc" : "blob-button-place-core-desc"));
        }
    }

    private bool IsValidTile(Entity<MapGridComponent> grid, Vector2i tile)
    {
        return _structure.IsStationGrid(grid) &&
               _map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) && !tileRef.Tile.IsEmpty;
    }

    /// <summary>check_core_visibility(): никого рядом и в поле зрения.</summary>
    private bool CheckCoreVisibility(EntityUid uid)
    {
        var coords = _transform.GetMapCoordinates(uid);
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var player, out _, out _, out var xform))
        {
            if (IsBlobAlly(player) || xform.MapID != coords.MapId)
                continue;

            var other = _transform.GetMapCoordinates(player, xform);
            var dist = (other.Position - coords.Position).Length();
            if (dist <= 7)
            {
                SendMessage(uid, Loc.GetString("blob-core-someone-close"));
                return false;
            }

            if (dist <= 13 && _examine.InRangeUnOccluded(other, coords, 13, null))
            {
                SendMessage(uid, Loc.GetString("blob-core-someone-see"));
                return false;
            }
        }

        return true;
    }

    /// <summary>check_objects_tile().</summary>
    private bool CheckObjectsTile(EntityUid uid, Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var atom in _structure.EntitiesOnTile(grid, tile))
        {
            if (atom == uid)
                continue;

            if (TryComp<BlobStructureComponent>(atom, out var blob))
            {
                if (blob.Type == BlobStructureType.Normal)
                {
                    QueueDel(atom);
                    continue;
                }

                SendMessage(uid, Loc.GetString("blob-core-already-blob"));
                return false;
            }

            if (TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(atom, out var physics) && physics.Hard && physics.CanCollide &&
                Transform(atom).Anchored)
            {
                SendMessage(uid, Loc.GetString("blob-core-too-dense"));
                return false;
            }
        }

        return true;
    }

    /// <summary>GLOB.blobstart: случайная вентиляция на станции.</summary>
    public EntityCoordinates? FindRandomBlobStart()
    {
        var locations = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<VentCritterSpawnLocationComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid is { } grid && _structure.IsStationGrid(grid))
                locations.Add(xform.Coordinates);
        }

        return locations.Count > 0 ? _random.Pick(locations) : null;
    }

    #endregion

    #region Клики

    // _onclick/overmind.dm: ЛКМ — разрастись, Ctrl — щит, средняя — споры, Alt — удалить.

    private bool TryGetOvermind(ICommonSession? session, out EntityUid uid, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BlobOvermindComponent? comp)
    {
        uid = default;
        comp = null;
        if (session?.AttachedEntity is not { } attached || !TryComp(attached, out comp))
            return false;
        uid = attached;
        return true;
    }

    /// <summary>get_turf(clicked_on): тайл кликнутой сущности или точки.</summary>
    private EntityCoordinates ClickTile(EntityCoordinates coords, EntityUid clicked)
    {
        return clicked.IsValid() && Exists(clicked) && !HasComp<MapGridComponent>(clicked) && !HasComp<MapComponent>(clicked)
            ? Transform(clicked).Coordinates
            : coords;
    }

    private bool HandleClick(ICommonSession? session, EntityCoordinates coords, EntityUid clicked)
    {
        if (!TryGetOvermind(session, out var uid, out var comp))
            return false;
        if (coords.IsValid(EntityManager))
            ExpandBlob(uid, comp, ClickTile(coords, clicked));
        return true;
    }

    private bool HandleCtrlClick(ICommonSession? session, EntityCoordinates coords, EntityUid clicked)
    {
        if (!TryGetOvermind(session, out var uid, out var comp))
            return false;
        if (coords.IsValid(EntityManager))
            CreateShield(uid, comp, ClickTile(coords, clicked));
        return true;
    }

    private bool HandleMiddleClick(ICommonSession? session, EntityCoordinates coords, EntityUid clicked)
    {
        if (!TryGetOvermind(session, out var uid, out var comp))
            return false;
        if (coords.IsValid(EntityManager))
            RallySpores(uid, comp, ClickTile(coords, clicked));
        return true;
    }

    private bool HandleAltClick(ICommonSession? session, EntityCoordinates coords, EntityUid clicked)
    {
        if (!TryGetOvermind(session, out var uid, out var comp))
            return false;
        if (coords.IsValid(EntityManager))
            RemoveBlob(uid, comp, ClickTile(coords, clicked));
        return true;
    }

    /// <summary>expand_blob(): ЛКМ оверманда.</summary>
    public void ExpandBlob(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates coords)
    {
        var now = _timing.CurTime;
        if (now < comp.LastAttack || !comp.Placed)
            return;

        if (coords.GetGridUid(EntityManager) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;
        var grid = new Entity<MapGridComponent>(gridUid, gridComp);
        var tile = _map.CoordinatesToTile(gridUid, gridComp, coords);

        var possible = _structure.BlobsInRange(grid, tile, 1);
        if (possible.Count == 0)
        {
            SendMessage(uid, Loc.GetString("blob-no-adjacent"));
            return;
        }

        if (!CanBuy(uid, comp, comp.ExpandCost))
            return;

        var attackSuccess = false;
        foreach (var mob in _structure.EntitiesOnTile(grid, tile))
        {
            if (!HasComp<MobStateComponent>(mob) || HasComp<GhostComponent>(mob) || IsBlobAlly(mob))
                continue;
            if (!_mobState.IsDead(mob))
                attackSuccess = true;
            _strain.AttackLiving(uid, mob, possible);
        }

        if (_structure.GetBlobAt(grid, tile) is { } existing)
        {
            if (attackSuccess)
            {
                _structure.AttackAnimation(existing, coords, uid);
                AddPoints(uid, comp, comp.AttackRefund);
            }
            else
            {
                SendMessage(uid, Loc.GetString("blob-already-there"));
                AddPoints(uid, comp, comp.ExpandCost);
            }
        }
        else
        {
            DirectionalAttack(uid, comp, grid, tile, possible, attackSuccess);
        }

        comp.LastAttack = now + (attackSuccess ? comp.AttackCooldown : comp.ExpandCooldown);
    }

    /// <summary>directional_attack(): кардинальные соседи растут, диагональные только бьют.</summary>
    private void DirectionalAttack(EntityUid uid, BlobOvermindComponent comp, Entity<MapGridComponent> grid, Vector2i tile, List<EntityUid> possible, bool attackSuccess)
    {
        var cardinal = new List<EntityUid>();
        var diagonal = new List<EntityUid>();
        foreach (var blob in possible)
        {
            if (!_structure.TryGetTile(blob, out _, out var blobTile))
                continue;
            if (blobTile.X == tile.X || blobTile.Y == tile.Y)
                cardinal.Add(blob);
            else
                diagonal.Add(blob);
        }

        if (cardinal.Count > 0)
        {
            var attacker = _random.Pick(cardinal);
            if (_structure.Expand((attacker, Comp<BlobStructureComponent>(attacker)), tile, uid) == null)
                AddPoints(uid, comp, comp.AttackRefund);
        }
        else if (diagonal.Count > 0)
        {
            var attacker = _random.Pick(diagonal);
            if (attackSuccess)
            {
                _structure.AttackAnimation(attacker, _structure.TileCenter(grid, tile), uid);
                _audio.PlayPvs(comp.SplatSound, attacker, AudioParams.Default.WithVolume(BlobStructureSystem.Db(50)).WithVariation(0.125f));
                AddPoints(uid, comp, comp.AttackRefund);
            }
            else
            {
                AddPoints(uid, comp, comp.ExpandCost);
            }
        }
    }

    #endregion

    #region Кнопки HUD

    private void OnJumpToCore(Entity<BlobOvermindComponent> ent, ref BlobJumpToCoreActionEvent args)
    {
        args.Handled = true;
        if (!ent.Comp.Placed)
            PlaceCore(ent, ent.Comp, BlobPlacement.Normal);

        // transport_core()
        if (ent.Comp.Core is { } core && !TerminatingOrDeleted(core))
        {
            _transform.SetCoordinates(ent, Transform(core).Coordinates);
            ent.Comp.LastValidPosition = Transform(core).Coordinates;
        }
    }

    private void OnJumpToNode(Entity<BlobOvermindComponent> ent, ref BlobJumpToNodeActionEvent args)
    {
        args.Handled = true;
        var nodes = new List<NetEntity>();
        var names = new List<string>();
        var index = 1;
        var query = EntityQueryEnumerator<BlobStructureComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Type != BlobStructureType.Node)
                continue;
            nodes.Add(GetNetEntity(uid));
            var area = _station.GetOwningStation(uid) is { } station ? Name(station) : Loc.GetString("blob-node-unknown-area");
            names.Add(Loc.GetString("blob-node-entry", ("index", index++), ("area", area)));
        }

        if (nodes.Count == 0)
            return;

        _ui.SetUiState(ent.Owner, BlobOvermindUiKey.Nodes, new BlobNodesState(nodes, names));
        _ui.TryOpenUi(ent.Owner, BlobOvermindUiKey.Nodes, ent);
    }

    private void OnJumpToNodeMessage(Entity<BlobOvermindComponent> ent, ref BlobJumpToNodeMessage args)
    {
        var node = GetEntity(args.Node);
        if (TryComp<BlobStructureComponent>(node, out var comp) && comp.Type == BlobStructureType.Node)
        {
            _transform.SetCoordinates(ent, Transform(node).Coordinates);
            ent.Comp.LastValidPosition = Transform(node).Coordinates;
        }

        _ui.CloseUi(ent.Owner, BlobOvermindUiKey.Nodes);
    }

    private void OnCreateResource(Entity<BlobOvermindComponent> ent, ref BlobCreateResourceActionEvent args)
    {
        args.Handled = true;
        CreateSpecial(ent, ent.Comp, ent.Comp.ResourceCost, ent.Comp.ResourcePrototype, BlobStructureType.Resource, ent.Comp.ResourceMinDistance, true);
    }

    private void OnCreateNode(Entity<BlobOvermindComponent> ent, ref BlobCreateNodeActionEvent args)
    {
        args.Handled = true;
        CreateSpecial(ent, ent.Comp, ent.Comp.NodeCost, ent.Comp.NodePrototype, BlobStructureType.Node, ent.Comp.NodeMinDistance, false);
    }

    private void OnCreateFactory(Entity<BlobOvermindComponent> ent, ref BlobCreateFactoryActionEvent args)
    {
        args.Handled = true;
        CreateSpecial(ent, ent.Comp, ent.Comp.FactoryCost, ent.Comp.FactoryPrototype, BlobStructureType.Factory, ent.Comp.FactoryMinDistance, true);
    }

    /// <summary>create_special().</summary>
    public EntityUid? CreateSpecial(EntityUid uid, BlobOvermindComponent comp, float price, EntProtoId proto, BlobStructureType type, int minSeparation, bool needsNode, EntityCoordinates? at = null)
    {
        var coords = at ?? Transform(uid).Coordinates;
        if (coords.GetGridUid(EntityManager) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return null;
        var grid = new Entity<MapGridComponent>(gridUid, gridComp);
        var tile = _map.CoordinatesToTile(gridUid, gridComp, coords);

        if (_structure.GetBlobAt(grid, tile) is not { } blob || !TryComp<BlobStructureComponent>(blob, out var blobComp))
        {
            SendMessage(uid, Loc.GetString("blob-no-blob-here"));
            _popup.PopupCoordinates(Loc.GetString("blob-no-blob-here-short"), coords, uid);
            return null;
        }

        if (blobComp.Type != BlobStructureType.Normal)
        {
            SendMessage(uid, Loc.GetString("blob-need-normal"));
            _popup.PopupEntity(Loc.GetString("blob-need-normal-short"), blob, uid);
            return null;
        }

        if (needsNode)
        {
            if (!_structure.IsStationGrid(gridUid))
            {
                SendMessage(uid, Loc.GetString("blob-must-be-on-station"));
                _popup.PopupEntity(Loc.GetString("blob-cant-place-off-station"), blob, uid);
                return null;
            }

            if (comp.NodesRequired && !HasSpecialNear(grid, tile, BlobStructureType.Node, comp.NodePulseRange) &&
                !HasSpecialNear(grid, tile, BlobStructureType.Core, comp.CorePulseRange))
            {
                SendMessage(uid, Loc.GetString("blob-need-node"));
                _popup.PopupEntity(Loc.GetString("blob-need-node-short"), blob, uid);
                return null;
            }
        }

        if (minSeparation > 0)
        {
            foreach (var other in _structure.BlobsInRange(grid, tile, minSeparation, includeCenter: false))
            {
                if (!TryComp<BlobStructureComponent>(other, out var otherComp) || otherComp.Type != type)
                    continue;
                SendMessage(uid, Loc.GetString("blob-similar-nearby", ("distance", minSeparation)));
                _popup.PopupEntity(Loc.GetString("blob-too-close"), other, uid);
                return null;
            }
        }

        if (!CanBuy(uid, comp, price))
            return null;

        return _structure.ChangeTo((blob, blobComp), proto, uid);
    }

    private bool HasSpecialNear(Entity<MapGridComponent> grid, Vector2i tile, BlobStructureType type, int range)
    {
        foreach (var blob in _structure.BlobsInRange(grid, tile, range, includeCenter: false))
        {
            if (TryComp<BlobStructureComponent>(blob, out var comp) && comp.Type == type)
                return true;
        }

        return false;
    }

    /// <summary>create_shield(): сильный блоб, затем отражающий.</summary>
    public void CreateShield(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates coords)
    {
        if (coords.GetGridUid(EntityManager) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;
        var grid = new Entity<MapGridComponent>(gridUid, gridComp);
        var tile = _map.CoordinatesToTile(gridUid, gridComp, coords);
        var blob = _structure.GetBlobAt(grid, tile);

        if (blob == null || !TryComp<BlobStructureComponent>(blob, out var shield) ||
            shield.Type is not (BlobStructureType.Strong or BlobStructureType.Reflective))
        {
            if (CreateSpecial(uid, comp, comp.StrongCost, comp.StrongPrototype, BlobStructureType.Strong, 0, false, coords) is { } created)
                _popup.PopupEntity(Loc.GetString("blob-upgraded", ("name", Name(created))), created, uid);
            return;
        }

        if (shield.Type == BlobStructureType.Reflective)
        {
            SendMessage(uid, Loc.GetString("blob-shield-max"));
            return;
        }

        if (shield.Integrity < shield.MaxIntegrity * 0.5f)
        {
            SendMessage(uid, Loc.GetString("blob-shield-damaged"));
            return;
        }

        if (!CanBuy(uid, comp, comp.ReflectorCost))
            return;

        SendMessage(uid, Loc.GetString("blob-shield-reflective"));
        if (_structure.ChangeTo((blob.Value, shield), comp.ReflectivePrototype, uid) is { } reflective)
            _popup.PopupEntity(Loc.GetString("blob-upgraded", ("name", Name(reflective))), reflective, uid);
    }

    private void OnCreateBlobbernaut(Entity<BlobOvermindComponent> ent, ref BlobCreateBlobbernautActionEvent args)
    {
        args.Handled = true;
        var comp = ent.Comp;
        if (!_structure.TryGetTile(ent, out var grid, out var tile) || _structure.GetBlobAt(grid, tile) is not { } factory ||
            !TryComp<BlobStructureComponent>(factory, out var factoryComp) || factoryComp.Type != BlobStructureType.Factory)
        {
            SendMessage(ent, Loc.GetString("blob-must-be-on-factory"));
            return;
        }

        if (factoryComp.Blobbernaut != null || factoryComp.CreatingBlobbernaut)
        {
            SendMessage(ent, Loc.GetString("blob-factory-sustaining"));
            return;
        }

        if (factoryComp.Integrity < factoryComp.MaxIntegrity * 0.5f)
        {
            SendMessage(ent, Loc.GetString("blob-factory-damaged"));
            return;
        }

        if (!CanBuy(ent, comp, comp.BlobbernautCost))
            return;

        factoryComp.CreatingBlobbernaut = true;
        SendMessage(ent, Loc.GetString("blob-blobbernaut-attempt"));
        _blobMob.StartBlobbernautPoll(ent, (factory, factoryComp));
    }

    private void OnRelocateCore(Entity<BlobOvermindComponent> ent, ref BlobRelocateCoreActionEvent args)
    {
        args.Handled = true;
        var comp = ent.Comp;
        if (!_structure.TryGetTile(ent, out var grid, out var tile) || _structure.GetBlobAt(grid, tile) is not { } node ||
            !TryComp<BlobStructureComponent>(node, out var nodeComp) || nodeComp.Type != BlobStructureType.Node)
        {
            SendMessage(ent, Loc.GetString("blob-must-be-on-node"));
            return;
        }

        if (comp.Core is not { } core || TerminatingOrDeleted(core))
        {
            SendMessage(ent, Loc.GetString("blob-no-core"));
            return;
        }

        if (!IsValidTile(grid, tile))
        {
            SendMessage(ent, Loc.GetString("blob-cannot-relocate"));
            return;
        }

        if (!CanBuy(ent, comp, comp.RelocateCost))
            return;

        _structure.SwapBlobs(core, node);
    }

    private void OnReadaptStrain(Entity<BlobOvermindComponent> ent, ref BlobReadaptStrainActionEvent args)
    {
        args.Handled = true;
        var comp = ent.Comp;
        if (comp.FreeRerolls <= 0 && comp.Points < comp.RerollCost)
        {
            SendMessage(ent, Loc.GetString("blob-reroll-need", ("cost", comp.RerollCost)));
            return;
        }

        if (comp.StrainChoices == null)
        {
            var strains = _proto.EnumeratePrototypes<BlobStrainPrototype>()
                .Where(s => s.Selectable && s.ID != comp.Strain)
                .Select(s => s.ID)
                .ToList();
            _random.Shuffle(strains);
            comp.StrainChoices = strains.Take(comp.RerollChoices).ToList();
        }

        _ui.SetUiState(ent.Owner, BlobOvermindUiKey.Reroll, new BlobRerollState(comp.StrainChoices));
        _ui.TryOpenUi(ent.Owner, BlobOvermindUiKey.Reroll, ent);
    }

    private void OnSelectStrain(Entity<BlobOvermindComponent> ent, ref BlobSelectStrainMessage args)
    {
        var comp = ent.Comp;
        _ui.CloseUi(ent.Owner, BlobOvermindUiKey.Reroll);
        if (comp.StrainChoices == null || !comp.StrainChoices.Contains(args.Strain) ||
            !_proto.HasIndex<BlobStrainPrototype>(args.Strain))
            return;

        if (comp.FreeRerolls <= 0 && !CanBuy(ent, comp, comp.RerollCost))
            return;

        SetStrain(ent, comp, args.Strain);
        if (comp.FreeRerolls > 0)
            comp.FreeRerolls--;
        comp.LastReroll = _timing.CurTime;
        comp.StrainChoices = null;
        Dirty(ent);
    }

    /// <summary>set_strain().</summary>
    public void SetStrain(EntityUid uid, BlobOvermindComponent comp, ProtoId<BlobStrainPrototype> strainId)
    {
        comp.Strain = strainId;
        Dirty(uid, comp);
        UpdateOvermindColor((uid, comp));

        foreach (var blob in comp.AllBlobs)
        {
            if (TryComp<BlobStructureComponent>(blob, out var blobComp))
                _structure.UpdateState(blob, blobComp);
        }

        foreach (var mob in comp.Mobs)
            _blobMob.OnStrainChanged(mob);

        if (GetStrain(comp) is { } strain)
            SendStrainInfo(uid, strain);
    }

    private void SendStrainInfo(EntityUid uid, BlobStrainPrototype strain)
    {
        var color = strain.Color.ToHex();
        var name = Loc.GetString(strain.Name);
        SendMessage(uid, Loc.GetString("blob-strain-now", ("color", color), ("name", name)));
        SendMessage(uid, Loc.GetString("blob-strain-desc", ("color", color), ("name", name), ("desc", Loc.GetString(strain.Description))));
        if (strain.EffectDesc is { } effect)
            SendMessage(uid, Loc.GetString("blob-strain-desc", ("color", color), ("name", name), ("desc", Loc.GetString(effect))));
    }

    /// <summary>rally_spores().</summary>
    public void RallySpores(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates target)
    {
        SendMessage(uid, Loc.GetString("blob-rally"));
        if (target.GetGridUid(EntityManager) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;

        var tile = _map.CoordinatesToTile(gridUid, gridComp, target);
        var targetMap = _transform.ToMapCoordinates(target);
        var neighbours = new List<EntityCoordinates>();
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;
                var neighbour = tile + new Vector2i(x, y);
                if (_map.TryGetTileRef(gridUid, gridComp, neighbour, out var tileRef) && !tileRef.Tile.IsEmpty)
                    neighbours.Add(_map.GridTileToLocal(gridUid, gridComp, neighbour));
            }
        }

        if (neighbours.Count == 0)
            return;

        foreach (var mob in comp.Mobs)
        {
            var xform = Transform(mob);
            if (xform.GridUid == null || HasComp<ActorComponent>(mob))
                continue;
            var pos = _transform.GetMapCoordinates(mob, xform);
            if (pos.MapId != targetMap.MapId || (pos.Position - targetMap.Position).Length() > 35)
                continue;
            if (!TryComp<HTNComponent>(mob, out var htn))
                continue;

            htn.Blackboard.Remove<EntityUid>("Target");
            _npc.SetBlackboard(mob, RallyKey, _random.Pick(neighbours), htn);
            _htn.Replan(htn);
            _npc.WakeNPC(mob, htn);
        }
    }

    /// <summary>remove_blob().</summary>
    public void RemoveBlob(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates coords)
    {
        if (coords.GetGridUid(EntityManager) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;
        var tile = _map.CoordinatesToTile(gridUid, gridComp, coords);
        if (_structure.GetBlobAt(gridUid, tile) is not { } blob || !TryComp<BlobStructureComponent>(blob, out var blobComp))
        {
            SendMessage(uid, Loc.GetString("blob-no-blob-there"));
            return;
        }

        if (blobComp.PointReturn < 0)
        {
            SendMessage(uid, Loc.GetString("blob-cannot-remove"));
            return;
        }

        if (comp.MaxPoints < blobComp.PointReturn + comp.Points)
        {
            SendMessage(uid, Loc.GetString("blob-too-many-resources"));
            return;
        }

        if (blobComp.PointReturn > 0)
        {
            AddPoints(uid, comp, blobComp.PointReturn);
            SendMessage(uid, Loc.GetString("blob-removed-gain", ("amount", blobComp.PointReturn), ("name", Name(blob))));
            _popup.PopupEntity(Loc.GetString("blob-resource-gained", ("amount", blobComp.PointReturn)), blob, uid);
        }

        QueueDel(blob);
    }

    #endregion

    #region Телепатия

    private void OnOvermindSpeech(Entity<BlobOvermindComponent> ent, ref TransformSpeechEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        var strain = GetStrain(ent.Comp);
        var wrapped = Loc.GetString("blob-telepathy-overmind",
            ("name", Name(ent)),
            ("color", strain?.Color.ToHex() ?? "#ffffff"),
            ("strain", strain != null ? Loc.GetString(strain.Name) : "?"),
            ("message", FormattedMessage.EscapeText(args.Message)));
        BlobTelepathy(ent, args.Message, wrapped);
        args.Message = string.Empty;
    }

    private void OnMinionSpeech(Entity<BlobMobComponent> ent, ref TransformSpeechEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        var wrapped = Loc.GetString("blob-telepathy-minion",
            ("name", Name(ent)),
            ("message", FormattedMessage.EscapeText(args.Message)));
        BlobTelepathy(ent, args.Message, wrapped);
        args.Message = string.Empty;
    }

    /// <summary>blob_talk(): сообщение всем блобам и призракам.</summary>
    private void BlobTelepathy(EntityUid source, string message, string wrapped)
    {
        var filter = Filter.Empty().AddWhereAttachedEntity(e =>
            HasComp<BlobOvermindComponent>(e) || HasComp<BlobMobComponent>(e) || HasComp<BlobInfectedComponent>(e) || HasComp<GhostComponent>(e));
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrapped, source, false, true, Color.FromHex("#4aa34a"));
    }

    #endregion

    public void SendMessage(EntityUid uid, string message)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, message, EntityUid.Invalid, false, actor.PlayerSession.Channel);
    }

    public string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;
        return time.TotalMinutes >= 1
            ? Loc.GetString("blob-time-minutes", ("minutes", (int) time.TotalMinutes), ("seconds", time.Seconds))
            : Loc.GetString("blob-time-seconds", ("seconds", (int) time.TotalSeconds));
    }
}
