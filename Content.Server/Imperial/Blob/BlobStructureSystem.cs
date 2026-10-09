using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Destructible;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Server.Station.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.Ghost;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mech.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Station.Components;
using Content.Shared.SubFloor;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Trigger.Components.Effects;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Blob;

/// <summary>
/// Структуры блоба (obj/structure/blob из SS13): рост, пульс ядра и узлов, урон и броня,
/// фабрики спор и ресурсные блобы.
/// </summary>
public sealed partial class BlobStructureSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly AirtightSystem _airtight = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly BlobOvermindSystem _overmind = default!;
    [Dependency] private readonly Content.Shared.Examine.ExamineSystemShared _examine = default!;
    [Dependency] private readonly BlobStrainSystem _strain = default!;
    [Dependency] private readonly BlobMobSystem _blobMob = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly EntProtoId NormalPrototype = "BlobNormal";
    private static readonly EntProtoId AttackEffect = "BlobAttackEffect";
    private static readonly EntProtoId ResourceGlowEffect = "BlobResourceGlowEffect";
    private static readonly EntProtoId EmpEffect = "BlobEmpEffect";
    private static readonly EntProtoId SmokeGrenadePrototype = "SmokeGrenade";
    private const string LatticeTile = "Lattice";

    /// <summary>SPT_PROB(BLOB_REINFORCE_CHANCE, 2): 1 - (1 - 0.025)^2.</summary>
    private const float ReinforceChance = 0.049375f;

    private static readonly Vector2i[] Cardinals = { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) };

    /// <summary>Тайлы с блобом: сетка → тайл → структура.</summary>
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, EntityUid>> _index = new();
    private readonly HashSet<EntityUid> _specials = new();
    private readonly HashSet<EntityUid> _tileEntities = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobStructureComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BlobStructureComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlobStructureComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BlobStructureComponent, GetExplosionResistanceEvent>(OnExplosionResistance);
        SubscribeLocalEvent<BlobStructureComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<SmokeOnTriggerComponent, EntityTerminatingEvent>(OnSmokeGrenadeTerminating);
        SubscribeLocalEvent<BlobStructureComponent, EmpPulseEvent>(OnEmp);
        SubscribeLocalEvent<BlobStructureComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<BlobStructureComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BlobStructureComponent, InteractUsingEvent>(OnInteractUsing);
    }

    #region Индекс тайлов

    public bool TryGetTile(EntityUid uid, out Entity<MapGridComponent> grid, out Vector2i tile)
    {
        var xform = Transform(uid);
        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var gridComp))
        {
            grid = (gridUid, gridComp);
            tile = _map.CoordinatesToTile(gridUid, gridComp, xform.Coordinates);
            return true;
        }

        grid = default;
        tile = default;
        return false;
    }

    public EntityUid? GetBlobAt(EntityUid grid, Vector2i tile)
    {
        if (_index.TryGetValue(grid, out var tiles) && tiles.TryGetValue(tile, out var blob) && !TerminatingOrDeleted(blob))
            return blob;
        return null;
    }

    /// <summary>Все блобы в квадрате радиуса range (range() из BYOND, включая центр).</summary>
    public List<EntityUid> BlobsInRange(EntityUid grid, Vector2i center, int range, bool includeCenter = true)
    {
        var result = new List<EntityUid>();
        if (!_index.TryGetValue(grid, out var tiles))
            return result;

        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                if (!includeCenter && x == 0 && y == 0)
                    continue;
                if (tiles.TryGetValue(center + new Vector2i(x, y), out var blob) && !TerminatingOrDeleted(blob))
                    result.Add(blob);
            }
        }

        return result;
    }

    public static int Chebyshev(Vector2i a, Vector2i b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    public EntityCoordinates TileCenter(Entity<MapGridComponent> grid, Vector2i tile) =>
        _map.GridTileToLocal(grid, grid.Comp, tile);

    private void AddToIndex(EntityUid uid)
    {
        if (!TryGetTile(uid, out var grid, out var tile))
            return;
        if (!_index.TryGetValue(grid, out var tiles))
            _index[grid] = tiles = new Dictionary<Vector2i, EntityUid>();
        tiles[tile] = uid;
    }

    private void RemoveFromIndex(EntityUid uid)
    {
        foreach (var tiles in _index.Values)
        {
            foreach (var (tile, blob) in tiles)
            {
                if (blob != uid)
                    continue;
                tiles.Remove(tile);
                return;
            }
        }
    }

    #endregion

    #region Жизненный цикл

    private void OnMapInit(Entity<BlobStructureComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Integrity < 0)
            ent.Comp.Integrity = ent.Comp.InitialIntegrity ?? ent.Comp.MaxIntegrity;

        AddToIndex(ent);
        if (ent.Comp.Type is BlobStructureType.Core or BlobStructureType.Node)
            _specials.Add(ent);

        ent.Comp.NextProcess = _timing.CurTime + ent.Comp.ProcessInterval;
        UpdateState(ent, ent.Comp);
    }

    private void OnShutdown(Entity<BlobStructureComponent> ent, ref ComponentShutdown args)
    {
        RemoveFromIndex(ent);
        _specials.Remove(ent);

        var xform = Transform(ent);
        if (xform.MapUid is { } mapUid && !TerminatingOrDeleted(mapUid))
            _audio.PlayPvs(ent.Comp.DestroySound, xform.Coordinates, AudioParams.Default.WithVolume(Db(50)).WithVariation(0.125f));

        if (ent.Comp.Type == BlobStructureType.Factory)
        {
            foreach (var spore in ent.Comp.Spores.ToList())
                _blobMob.OnFactoryDestroyed(spore);
            ent.Comp.Spores.Clear();
            if (ent.Comp.Blobbernaut is { } naut)
                _blobMob.OnFactoryDestroyed(naut);
        }

        if (ent.Comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om))
            Unregister(overmind, om, ent);
    }

    /// <summary>Привязка структуры к оверманду (overmind.all_blobs и списки особых блобов).</summary>
    public void Register(EntityUid overmind, BlobOvermindComponent om, Entity<BlobStructureComponent> ent)
    {
        ent.Comp.Overmind = overmind;
        Dirty(ent);
        om.AllBlobs.Add(ent);
        switch (ent.Comp.Type)
        {
            case BlobStructureType.Node:
                om.Nodes.Add(ent);
                break;
            case BlobStructureType.Factory:
                om.Factories.Add(ent);
                break;
            case BlobStructureType.Resource:
                om.Resources.Add(ent);
                break;
        }

        if (ent.Comp.Legit)
            om.BlobsLegit.Add(ent);
        om.BlobCount = om.BlobsLegit.Count;
    }

    private void Unregister(EntityUid overmind, BlobOvermindComponent om, EntityUid uid)
    {
        om.AllBlobs.Remove(uid);
        om.BlobsLegit.Remove(uid);
        om.Nodes.Remove(uid);
        om.Factories.Remove(uid);
        om.Resources.Remove(uid);
        if (om.Core == uid)
        {
            om.Core = null;
            om.CoreHealth = -1;
        }

        om.BlobCount = om.BlobsLegit.Count;
        Dirty(overmind, om);
    }

    /// <summary>Отвязка от оверманда (cleanup_overmind_blobs): блоб становится «мёртвым».</summary>
    public void ClearOvermind(Entity<BlobStructureComponent> ent)
    {
        ent.Comp.Overmind = null;
        Dirty(ent);
        UpdateState(ent, ent.Comp);
    }

    /// <summary>
    /// Создаёт структуру блоба на тайле (new /obj/structure/blob/...(loc, overmind)).
    /// </summary>
    public EntityUid SpawnBlob(EntProtoId proto, Entity<MapGridComponent> grid, Vector2i tile, EntityUid? overmind, Angle? rotation = null, int? pointReturn = null)
    {
        var coords = TileCenter(grid, tile);
        var uid = Spawn(proto, coords);
        _transform.SetLocalRotation(uid, rotation ?? Angle.FromDegrees(90 * _random.Next(4)));

        var comp = EnsureComp<BlobStructureComponent>(uid);
        if (pointReturn != null)
            comp.PointReturn = pointReturn.Value;

        comp.Legit = IsStationGrid(grid);
        if (overmind != null && TryComp<BlobOvermindComponent>(overmind, out var om))
            Register(overmind.Value, om, (uid, comp));

        if (comp.AtmosBlock && TryComp<AirtightComponent>(uid, out var airtight))
            _airtight.SetAirblocked((uid, airtight), true);

        UpdateState(uid, comp);
        ConsumeTile(uid, comp);
        return uid;
    }

    /// <summary>change_to(): заменить блоб другим типом на том же месте.</summary>
    public EntityUid? ChangeTo(Entity<BlobStructureComponent> ent, EntProtoId proto, EntityUid? overmind, int? pointReturn = null)
    {
        if (!TryGetTile(ent, out var grid, out var tile))
            return null;

        var rotation = Transform(ent).LocalRotation;
        RemoveFromIndex(ent);
        QueueDel(ent);
        return SpawnBlob(proto, grid, tile, overmind, rotation, pointReturn);
    }

    public bool IsStationGrid(EntityUid grid) => HasComp<StationMemberComponent>(grid);

    /// <summary>Переместить блоб на тайл (forceMove).</summary>
    public void MoveBlob(EntityUid uid, Entity<MapGridComponent> grid, Vector2i tile)
    {
        RemoveFromIndex(uid);
        var xform = Transform(uid);
        if (xform.Anchored)
            _transform.Unanchor(uid, xform);
        _transform.SetCoordinates(uid, TileCenter(grid, tile));
        _transform.AnchorEntity((uid, Transform(uid)), grid);
        AddToIndex(uid);
    }

    /// <summary>Поменять два блоба местами.</summary>
    public void SwapBlobs(EntityUid a, EntityUid b)
    {
        if (!TryGetTile(a, out var gridA, out var tileA) || !TryGetTile(b, out var gridB, out var tileB) || gridA.Owner != gridB.Owner)
            return;

        RemoveFromIndex(a);
        RemoveFromIndex(b);
        foreach (var uid in new[] { a, b })
        {
            var xform = Transform(uid);
            if (xform.Anchored)
                _transform.Unanchor(uid, xform);
        }

        _transform.SetCoordinates(a, TileCenter(gridA, tileB));
        _transform.SetCoordinates(b, TileCenter(gridA, tileA));
        _transform.AnchorEntity((a, Transform(a)), gridA);
        _transform.AnchorEntity((b, Transform(b)), gridA);
        AddToIndex(a);
        AddToIndex(b);
    }

    #endregion

    #region Процессы ядра и узлов

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        foreach (var uid in _specials.ToArray())
        {
            if (TerminatingOrDeleted(uid) || !TryComp<BlobStructureComponent>(uid, out var comp))
            {
                _specials.Remove(uid);
                continue;
            }

            if (now < comp.NextProcess)
                continue;
            comp.NextProcess = now + comp.ProcessInterval;

            if (comp.Type == BlobStructureType.Core)
                ProcessCore((uid, comp));
            else
                ProcessNode((uid, comp));
        }
    }

    /// <summary>core/process().</summary>
    private void ProcessCore(Entity<BlobStructureComponent> core)
    {
        if (core.Comp.Overmind is not { } overmind || !TryComp<BlobOvermindComponent>(overmind, out var om))
        {
            QueueDel(core);
            return;
        }

        _strain.CoreProcess(overmind, om, core);
        _overmind.UpdateHud(overmind, om);
        PulseArea(core, overmind, core.Comp.ClaimRange, core.Comp.PulseRange, core.Comp.ExpandRange);
        ReinforceArea(core);
    }

    /// <summary>node/process().</summary>
    private void ProcessNode(Entity<BlobStructureComponent> node)
    {
        if (node.Comp.Overmind is not { } overmind || !HasComp<BlobOvermindComponent>(overmind))
            return;

        PulseArea(node, overmind, node.Comp.ClaimRange, node.Comp.PulseRange, node.Comp.ExpandRange);
        ReinforceArea(node);
    }

    /// <summary>reinforce_area(): превращение соседей в сильные и отражающие блобы.</summary>
    private void ReinforceArea(Entity<BlobStructureComponent> ent)
    {
        if (ent.Comp.Overmind is not { } overmind || !TryComp<BlobOvermindComponent>(overmind, out var om) ||
            !TryGetTile(ent, out var grid, out var tile))
            return;

        if (ent.Comp.StrongReinforceRange > 0)
        {
            foreach (var blob in BlobsInRange(grid, tile, ent.Comp.StrongReinforceRange))
            {
                if (TryComp<BlobStructureComponent>(blob, out var comp) && comp.Type == BlobStructureType.Normal &&
                    _random.Prob(ReinforceChance))
                    ChangeTo((blob, comp), om.StrongPrototype, overmind, 0);
            }
        }

        if (ent.Comp.ReflectorReinforceRange > 0)
        {
            foreach (var blob in BlobsInRange(grid, tile, ent.Comp.ReflectorReinforceRange))
            {
                if (TryComp<BlobStructureComponent>(blob, out var comp) &&
                    comp.Type is BlobStructureType.Strong && _random.Prob(ReinforceChance))
                    ChangeTo((blob, comp), om.ReflectivePrototype, overmind, 0);
            }
        }
    }

    /// <summary>pulse_area(): пульс, лечение, перехват мёртвых блобов и естественный рост.</summary>
    public void PulseArea(Entity<BlobStructureComponent> ent, EntityUid? pulsingOvermind, int claimRange, int pulseRange, int expandRange)
    {
        if (pulsingOvermind == null || TerminatingOrDeleted(pulsingOvermind.Value))
            pulsingOvermind = ent.Comp.Overmind;

        BePulsed(ent);

        var expanded = false;
        if (_random.Prob(0.7f) && Expand(ent, null, null) != null)
            expanded = true;

        if (!TryGetTile(ent, out var grid, out var center))
            return;

        var blobs = BlobsInRange(grid, center, claimRange, includeCenter: false);
        _random.Shuffle(blobs);
        var now = _timing.CurTime;

        foreach (var blob in blobs)
        {
            if (TerminatingOrDeleted(blob) || !TryComp<BlobStructureComponent>(blob, out var comp) ||
                !TryGetTile(blob, out _, out var tile))
                continue;

            if (comp.Overmind == null && pulsingOvermind is { } claimer && _random.Prob(0.3f) &&
                TryComp<BlobOvermindComponent>(claimer, out var claimerComp))
            {
                Register(claimer, claimerComp, (blob, comp));
                UpdateState(blob, comp);
            }

            var distance = Chebyshev(center, tile);
            var expandProbability = distance <= 1 ? 20f : Math.Max(20 - distance * 8, 1);

            if (distance <= expandRange)
            {
                var canExpand = !(blobs.Count >= 120 && now < comp.HealCooldown);
                if (canExpand && now >= comp.PulseCooldown && _random.Prob(expandProbability / 100f) && !expanded)
                {
                    if (Expand((blob, comp), null, null) != null)
                        expanded = true;
                }
            }

            if (distance <= pulseRange)
                BePulsed((blob, comp));
        }
    }

    /// <summary>Be_Pulsed(): поглотить тайл, подлечиться; фабрики и ресурсные блобы работают от пульса.</summary>
    public bool BePulsed(Entity<BlobStructureComponent> ent)
    {
        var now = _timing.CurTime;
        var pulsed = false;
        if (now >= ent.Comp.PulseCooldown)
        {
            ConsumeTile(ent, ent.Comp);
            if (TerminatingOrDeleted(ent))
                return false;

            if (now >= ent.Comp.HealCooldown)
            {
                ent.Comp.Integrity = Math.Min(ent.Comp.MaxIntegrity, ent.Comp.Integrity + ent.Comp.HealthRegen);
                ent.Comp.HealCooldown = now + TimeSpan.FromSeconds(2);
            }

            UpdateState(ent, ent.Comp);
            ent.Comp.PulseCooldown = now + TimeSpan.FromSeconds(1);
            pulsed = true;
        }

        switch (ent.Comp.Type)
        {
            case BlobStructureType.Factory:
                FactoryPulse(ent, now);
                break;
            case BlobStructureType.Resource:
                ResourcePulse(ent, now);
                break;
        }

        return pulsed;
    }

    private void FactoryPulse(Entity<BlobStructureComponent> factory, TimeSpan now)
    {
        if (factory.Comp.Blobbernaut != null || factory.Comp.Spores.Count >= factory.Comp.MaxSpores || now < factory.Comp.NextSpore)
            return;

        factory.Comp.NextSpore = now + factory.Comp.SporeCooldown;
        var spore = _blobMob.CreateSpore(factory.Comp.Overmind, Transform(factory).Coordinates, BlobMobType.Spore);
        RegisterFactoryMob(factory, spore);
    }

    /// <summary>register_mob(): фабрика следит за своими спорами и зомби.</summary>
    public void RegisterFactoryMob(Entity<BlobStructureComponent> factory, EntityUid mob)
    {
        factory.Comp.Spores.Add(mob);
        if (TryComp<BlobMobComponent>(mob, out var blobMob))
            blobMob.Factory = factory;
    }

    /// <summary>on_spore_died: смерть споры сбрасывает таймер, чтобы её не заменили мгновенно.</summary>
    public void OnFactoryMobDied(EntityUid factory, EntityUid mob)
    {
        if (!TryComp<BlobStructureComponent>(factory, out var comp))
            return;
        comp.NextSpore = _timing.CurTime + comp.SporeCooldown;
    }

    public void OnFactoryMobRemoved(EntityUid factory, EntityUid mob)
    {
        if (TryComp<BlobStructureComponent>(factory, out var comp))
            comp.Spores.Remove(mob);
    }

    private void ResourcePulse(Entity<BlobStructureComponent> resource, TimeSpan now)
    {
        if (now < resource.Comp.NextGather)
            return;

        SpawnTinted(ResourceGlowEffect, Transform(resource).Coordinates, GetColor(resource.Comp), Transform(resource).LocalRotation);
        if (resource.Comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om))
        {
            _overmind.AddPoints(overmind, om, resource.Comp.GatherAmount);
            _popup.PopupEntity(Loc.GetString("blob-resource-gained", ("amount", resource.Comp.GatherAmount)), resource, overmind);
            resource.Comp.NextGather = now + resource.Comp.GatherDelay + resource.Comp.GatherAddedDelay * om.Resources.Count;
        }
        else
        {
            resource.Comp.NextGather = now + resource.Comp.GatherDelay;
        }
    }

    /// <summary>Фабрика получила блоббернаута: max_integrity * 0.25.</summary>
    public void AssignBlobbernaut(Entity<BlobStructureComponent> factory, EntityUid? naut)
    {
        factory.Comp.CreatingBlobbernaut = false;
        if (naut == null)
            return;

        var proto = MetaData(factory).EntityPrototype;
        var baseMax = proto != null && proto.TryGetComponent<BlobStructureComponent>(out var protoComp, EntityManager.ComponentFactory)
            ? protoComp.MaxIntegrity
            : factory.Comp.MaxIntegrity;
        factory.Comp.MaxIntegrity = baseMax * 0.25f;
        factory.Comp.Integrity = Math.Min(factory.Comp.Integrity, factory.Comp.MaxIntegrity);
        factory.Comp.Blobbernaut = naut;
        _popup.PopupEntity(Loc.GetString("blob-blobbernaut-rips-out", ("verb", Loc.GetString(_random.Pick(new[] { "blob-verb-rips", "blob-verb-tears", "blob-verb-shreds" })))), factory, PopupType.MediumCaution);
        _audio.PlayPvs(factory.Comp.DestroySound, factory, AudioParams.Default.WithVolume(Db(50)).WithVariation(0.125f));
    }

    /// <summary>on_blobbernaut_death: возвращает фабрике прочность.</summary>
    public void OnBlobbernautDied(EntityUid factory, EntityUid naut)
    {
        if (!TryComp<BlobStructureComponent>(factory, out var comp) || comp.Blobbernaut != naut)
            return;

        comp.Blobbernaut = null;
        var proto = MetaData(factory).EntityPrototype;
        if (proto != null && proto.TryGetComponent<BlobStructureComponent>(out var protoComp, EntityManager.ComponentFactory))
            comp.MaxIntegrity = protoComp.MaxIntegrity;
    }

    #endregion

    #region Рост и атака

    /// <summary>
    /// expand(): попытка разрастись на тайл T (или случайный кардинальный).
    /// Возвращает новый блоб или null.
    /// </summary>
    public EntityUid? Expand(Entity<BlobStructureComponent> ent, Vector2i? target, EntityUid? controller, bool expandReaction = true)
    {
        if (TerminatingOrDeleted(ent) || !TryGetTile(ent, out var grid, out var origin))
            return null;

        if (target == null)
        {
            var dirs = Cardinals.ToList();
            _random.Shuffle(dirs);
            foreach (var dir in dirs)
            {
                if (GetBlobAt(grid, origin + dir) != null)
                    continue;
                target = origin + dir;
                break;
            }
        }

        if (target is not { } tile)
            return null;

        var makeBlob = true;
        if (controller != null && TryComp<BlobOvermindComponent>(controller, out var controllerComp) && !CanMakeBlob(controllerComp))
        {
            _popup.PopupEntity(Loc.GetString("blob-max-tiles"), ent, controller.Value);
            makeBlob = false;
        }

        var isSpace = IsSpace(grid, tile);
        if (isSpace && _random.Prob(0.8f))
        {
            makeBlob = false;
            _audio.PlayPvs(ent.Comp.DestroySound, ent, AudioParams.Default.WithVolume(Db(50)).WithVariation(0.125f));
            if (controller != null)
                _popup.PopupCoordinates(Loc.GetString("blob-expand-failed"), TileCenter(grid, tile), controller.Value);
        }

        ConsumeTile(ent, ent.Comp);
        if (TerminatingOrDeleted(ent))
            return null;

        var overmind = controller ?? ent.Comp.Overmind;
        foreach (var atom in EntitiesOnTile(grid, tile))
        {
            if (TerminatingOrDeleted(atom) || HasComp<BlobStructureComponent>(atom))
                continue;
            if (Blocks(atom))
                makeBlob = false;
            if (!CanBlobAttack(atom))
                continue;
            if (HasComp<MobStateComponent>(atom) && ent.Comp.Overmind != null && controller == null)
            {
                if (!_overmind.IsBlobAlly(atom))
                    _strain.AttackLiving(ent.Comp.Overmind, atom, null);
                continue;
            }

            BlobAct(atom, ent);
        }

        if (!makeBlob || GetBlobAt(grid, tile) != null)
        {
            AttackAnimation(ent, TileCenter(grid, tile), controller);
            return null;
        }

        // T.Enter(B): после ударов клетка могла остаться занятой.
        foreach (var atom in EntitiesOnTile(grid, tile))
        {
            if (TerminatingOrDeleted(atom) || !Blocks(atom))
                continue;
            AttackAnimation(ent, TileCenter(grid, tile), controller);
            return null;
        }

        if (isSpace)
        {
            if (!_tileDefs.TryGetDefinition(LatticeTile, out var lattice))
                return null;
            _map.SetTile(grid, grid.Comp, tile, new Tile(lattice.TileId));
        }

        var newBlob = SpawnBlob(NormalPrototype, grid, tile, overmind);
        var newComp = Comp<BlobStructureComponent>(newBlob);
        if (controller != null && !newComp.Legit)
            _popup.PopupEntity(Loc.GetString("blob-off-station"), newBlob, controller.Value);

        if (newComp.Overmind != null && expandReaction)
            _strain.ExpandReaction(ent, (newBlob, newComp), grid, tile, controller);

        return TerminatingOrDeleted(newBlob) ? null : newBlob;
    }

    /// <summary>can_make_blob(): при отключённой победе блоб не растёт дальше критической массы.</summary>
    private static bool CanMakeBlob(BlobOvermindComponent om) => om.EndRoundOnVictory || om.BlobsLegit.Count < om.WinCount;

    private bool IsSpace(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef))
            return true;
        return tileRef.Tile.IsEmpty;
    }

    public HashSet<EntityUid> EntitiesOnTile(Entity<MapGridComponent> grid, Vector2i tile)
    {
        _tileEntities.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid, tile, _tileEntities, -0.05f, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries, grid.Comp);
        return new HashSet<EntityUid>(_tileEntities);
    }

    /// <summary>CanPass(): плотные объекты и мобы не дают блобу занять клетку.</summary>
    private bool Blocks(EntityUid uid)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics) || !physics.CanCollide || !physics.Hard)
            return false;
        if (HasComp<MobStateComponent>(uid))
            return true;
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return false;

        const int blocking = (int) (CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable | CollisionGroup.LowImpassable);
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard && (fixture.CollisionLayer & blocking) != 0)
                return true;
        }

        return false;
    }

    /// <summary>can_blob_attack(): призраки и нематериальные сущности неуязвимы.</summary>
    private bool CanBlobAttack(EntityUid uid)
    {
        if (HasComp<GhostComponent>(uid) || HasComp<BlobOvermindComponent>(uid))
            return false;
        var xform = Transform(uid);
        return xform.ParentUid == xform.GridUid;
    }

    /// <summary>ConsumeTile(): бьёт всё на своей клетке.</summary>
    public void ConsumeTile(EntityUid uid, BlobStructureComponent comp)
    {
        if (!TryGetTile(uid, out var grid, out var tile))
            return;

        foreach (var atom in EntitiesOnTile(grid, tile))
        {
            if (atom == uid || TerminatingOrDeleted(atom) || HasComp<BlobStructureComponent>(atom) || !CanBlobAttack(atom))
                continue;

            if (HasComp<MobStateComponent>(atom) && comp.Overmind != null && !_overmind.IsBlobAlly(atom))
            {
                _strain.AttackLiving(comp.Overmind, atom, null);
                continue;
            }

            BlobAct(atom, (uid, comp));
            if (TerminatingOrDeleted(uid))
                return;
        }
    }

    /// <summary>blob_act(): реакция чего угодно на удар блоба. Возвращает true, если удар что-то сделал.</summary>
    public bool BlobAct(EntityUid target, Entity<BlobStructureComponent> blob)
    {
        if (TerminatingOrDeleted(target) || HasComp<BlobStructureComponent>(target))
            return false;

        // datum/component/blob_minion: миньоны лечатся от касания блоба.
        if (HasComp<BlobMobComponent>(target))
            return _blobMob.HealFromBlob(target);

        // Заражённые (зомби блоба) и прочие члены фракции блоба не атакуются.
        if (HasComp<MobStateComponent>(target) && _overmind.IsBlobAlly(target))
            return false;

        if (HasComp<MobStateComponent>(target))
            return MobBlobAct(target);

        if (HasComp<MechComponent>(target))
        {
            Damage(target, Blunt, 30, false);
            return true;
        }

        // Подпольные коммуникации не трогаем (TRAIT_UNDERFLOOR).
        if (TryComp<SubFloorHideComponent>(target, out var subfloor) && subfloor.IsUnderCover)
            return false;

        // turf/closed/mineral: prob(50) — разрушить породу.
        if (HasComp<GatherableComponent>(target) && _tag.HasTag(target, WallTag))
        {
            if (_random.Prob(0.5f))
                _gatherable.Gather(target);
            return true;
        }

        // turf/closed/wall: prob(50) — разобрать стену.
        if (_tag.HasTag(target, WallTag))
        {
            if (_random.Prob(0.5f))
                DestroyObject(target);
            return true;
        }

        // obj/item: уничтожается только на клетке самого блоба.
        if (HasComp<ItemComponent>(target))
        {
            if (Transform(target).Coordinates.TryDistance(EntityManager, Transform(blob).Coordinates, out var dist) && dist < 0.5f &&
                !IsIndestructible(target))
            {
                QueueDel(target);
                return true;
            }

            return false;
        }

        // obj: take_damage(400, BRUTE, MELEE).
        if (HasComp<DamageableComponent>(target))
        {
            Damage(target, Blunt, 400, false);
            return true;
        }

        return false;
    }

    private bool MobBlobAct(EntityUid target)
    {
        if (TryComp<BorgChassisComponent>(target, out _))
        {
            if (_mobState.IsDead(target))
                _gibbing.Gib(target);
            else
                Damage(target, Blunt, 30, true);
            return true;
        }

        if (HasComp<HumanoidProfileComponent>(target))
        {
            if (_mobState.IsDead(target))
                return false;
            _popup.PopupEntity(Loc.GetString("blob-attacks-you"), target, target, PopupType.LargeCaution);
            Damage(target, Blunt, 5, false);
            return true;
        }

        Damage(target, Blunt, 20, true);
        return true;
    }

    private bool IsIndestructible(EntityUid uid)
    {
        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
            return true;
        var proto = MetaData(uid).EntityPrototype?.ID;
        return proto != null && proto.Contains("NukeDisk");
    }

    /// <summary>Полное разрушение объекта с его обычными последствиями (обломки, балки).</summary>
    private void DestroyObject(EntityUid uid)
    {
        var amount = _destructible.DestroyedAt(uid);
        if (amount <= 0 || amount == Content.Shared.FixedPoint.FixedPoint2.MaxValue)
        {
            QueueDel(uid);
            return;
        }

        var current = _damageable.GetTotalDamage(uid);
        Damage(uid, Blunt, (float) (amount - current) + 1, true);
    }

    public void Damage(EntityUid target, ProtoId<DamageTypePrototype> type, float amount, bool ignoreResistances, EntityUid? origin = null)
    {
        if (amount <= 0)
            return;
        _damageable.TryChangeDamage(target, new DamageSpecifier(_proto.Index(type), amount), ignoreResistances, origin: origin);
    }

    /// <summary>blob_attack_animation(): короткая вспышка в сторону цели.</summary>
    public void AttackAnimation(EntityUid blob, EntityCoordinates target, EntityUid? controller = null)
    {
        if (TerminatingOrDeleted(blob) || !TryComp<BlobStructureComponent>(blob, out var comp))
            return;

        var from = _transform.ToMapCoordinates(Transform(blob).Coordinates).Position;
        var to = _transform.ToMapCoordinates(target).Position;
        var dir = to - from;
        var color = GetColor(comp, controller);
        var effect = SpawnTinted(AttackEffect, Transform(blob).Coordinates,
            color.WithAlpha(controller != null ? 200 / 255f : 140 / 255f), Transform(blob).LocalRotation);

        // do_attack_animation: 8 px по каждой оси в сторону цели.
        var lunge = new Vector2(MathF.Sign(MathF.Round(dir.X, 2)), MathF.Sign(MathF.Round(dir.Y, 2))) * 0.25f;
        var attack = EnsureComp<BlobAttackEffectComponent>(effect);
        attack.Lunge = (-_transform.GetWorldRotation(effect)).RotateVec(lunge);
        Dirty(effect, attack);
    }

    public EntityUid SpawnTinted(EntProtoId proto, EntityCoordinates coords, Color color, Angle rotation = default)
    {
        var uid = Spawn(proto, coords);
        _transform.SetLocalRotation(uid, rotation);
        _appearance.SetData(uid, BlobVisuals.Color, color);
        return uid;
    }

    #endregion

    #region Урон

    private void OnExplosionResistance(Entity<BlobStructureComponent> ent, ref GetExplosionResistanceEvent args)
    {
        ent.Comp.LastExplosionTick = _timing.CurTick;
    }

    private void OnAttacked(Entity<BlobStructureComponent> ent, ref AttackedEvent args)
    {
        ent.Comp.LastMeleeTick = _timing.CurTick;
    }

    /// <summary>
    /// grenade/smokebomb/detonate(): дымовая граната бьёт блобы в поле зрения (8 тайлов)
    /// на round(30 / (dist + 1)) ожогом с флагом MELEE.
    /// </summary>
    private void OnSmokeGrenadeTerminating(Entity<SmokeOnTriggerComponent> ent, ref EntityTerminatingEvent args)
    {
        if (MetaData(ent).EntityPrototype?.ID != SmokeGrenadePrototype.Id)
            return;

        var xform = Transform(ent);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map) || xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;

        var origin = _transform.GetMapCoordinates(ent, xform);
        var center = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        foreach (var blob in BlobsInRange(gridUid, center, 8))
        {
            if (!TryComp<BlobStructureComponent>(blob, out var comp) || !TryGetTile(blob, out _, out var tile))
                continue;
            var target = _transform.GetMapCoordinates(blob);
            if (!_examine.InRangeUnOccluded(origin, target, 8.5f, e => e == blob))
                continue;
            var damage = MathF.Round(30f / (Chebyshev(center, tile) + 1));
            TakeDamage((blob, comp), damage, false, BlobDamageFlag.Melee, false);
        }
    }

    /// <summary>
    /// Весь урон по блобу переводится в atom_integrity по правилам run_atom_armor().
    /// </summary>
    private void OnDamageChanged(Entity<BlobStructureComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.IgnoreDamage || args.DamageDelta is not { } delta || !args.DamageIncreased)
            return;

        ent.Comp.IgnoreDamage = true;
        _damageable.SetAllDamage((ent.Owner, args.Damageable), 0);
        ent.Comp.IgnoreDamage = false;

        // attack_animal: миньоны и союзники блоба не бьют его структуры.
        if (args.Origin is { } origin && _overmind.IsBlobAlly(origin))
            return;

        float brute = 0, burn = 0, shock = 0, acid = 0;
        foreach (var (type, value) in delta.DamageDict)
        {
            if (value <= 0)
                continue;
            switch (type)
            {
                case "Blunt" or "Slash" or "Piercing" or "Structural":
                    brute += value.Float();
                    break;
                case "Heat" or "Cold":
                    burn += value.Float();
                    break;
                case "Shock":
                    shock += value.Float();
                    break;
                case "Caustic":
                    acid += value.Float();
                    break;
            }
        }

        var bomb = ent.Comp.LastExplosionTick == _timing.CurTick;
        var melee = ent.Comp.LastMeleeTick == _timing.CurTick;
        var direct = args.Origin != null;

        if (bomb && ent.Comp.Type == BlobStructureType.Core)
        {
            // core/ex_act: 10 * (severity + 1) — не больше 40.
            brute = Math.Min(brute + burn + shock + acid, 40f);
            burn = shock = acid = 0;
        }

        // Поправка под оружие SS14 (не для взрывов и огня).
        if (!bomb && direct)
        {
            brute *= melee ? ent.Comp.MeleeBruteMultiplier : ent.Comp.RangedBruteMultiplier;
            burn *= melee ? ent.Comp.MeleeBurnMultiplier : ent.Comp.RangedBurnMultiplier;
        }

        if (brute > 0)
            TakeDamage(ent, brute, true, bomb ? BlobDamageFlag.Bomb : melee || !direct ? BlobDamageFlag.Melee : BlobDamageFlag.Bullet);
        if (burn > 0)
            TakeDamage(ent, burn, false, bomb ? BlobDamageFlag.Bomb : melee ? BlobDamageFlag.Melee : direct ? BlobDamageFlag.Laser : BlobDamageFlag.Fire);
        if (shock > 0 && !_strain.IgnoresShock(ent.Comp.Overmind))
            TakeDamage(ent, shock, false, BlobDamageFlag.Energy);
        if (acid > 0)
            TakeDamage(ent, acid, false, BlobDamageFlag.Acid);
    }

    /// <summary>take_damage() → run_atom_armor() → damage_reaction().</summary>
    public float TakeDamage(Entity<BlobStructureComponent> ent, float amount, bool brute, BlobDamageFlag flag, bool sound = true)
    {
        if (TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
            return 0;

        if (sound)
            PlayAttackSound(ent, amount, brute);

        var damage = RunArmor(ent, amount, brute, flag);
        if (damage < 0.1f || TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
            return 0;

        ent.Comp.Integrity -= damage;
        if (ent.Comp.Integrity <= 0)
        {
            DestroyBlob(ent, flag);
            return damage;
        }

        // shield/take_damage: atmosblock = atom_integrity < max_integrity * 0.5.
        if (ent.Comp.Type is BlobStructureType.Strong or BlobStructureType.Reflective)
        {
            ent.Comp.AtmosBlock = ent.Comp.Integrity < ent.Comp.MaxIntegrity * 0.5f;
            if (TryComp<AirtightComponent>(ent, out var airtight))
                _airtight.SetAirblocked((ent, airtight), ent.Comp.AtmosBlock);
        }

        UpdateState(ent, ent.Comp);
        if (ent.Comp.Type == BlobStructureType.Core && ent.Comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om))
            _overmind.UpdateHud(overmind, om);

        return damage;
    }

    private float RunArmor(Entity<BlobStructureComponent> ent, float amount, bool brute, BlobDamageFlag flag)
    {
        amount *= brute ? ent.Comp.BruteResist : ent.Comp.FireResist;
        var armor = flag != BlobDamageFlag.None ? ent.Comp.Armor.GetValueOrDefault(flag) : 0f;
        amount = MathF.Round(amount * (100 - armor) * 0.01f, 1);
        if (ent.Comp.Overmind != null && flag != BlobDamageFlag.None)
            amount = _strain.DamageReaction(ent, amount, brute, flag);
        return amount;
    }

    /// <summary>play_attack_sound().</summary>
    private void PlayAttackSound(Entity<BlobStructureComponent> ent, float amount, bool brute)
    {
        if (brute)
        {
            _audio.PlayPvs(amount > 0 ? ent.Comp.BruteHitSound : ent.Comp.TapSound, ent,
                AudioParams.Default.WithVolume(Db(50)).WithVariation(0.125f));
        }
        else
        {
            _audio.PlayPvs(ent.Comp.BurnHitSound, ent, AudioParams.Default.WithVariation(0.125f));
        }
    }

    /// <summary>atom_destruction(): реакция штамма на гибель и удаление.</summary>
    public void DestroyBlob(Entity<BlobStructureComponent> ent, BlobDamageFlag flag)
    {
        if (TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
            return;

        if (ent.Comp.Overmind != null)
            _strain.DeathReaction(ent, flag);
        QueueDel(ent);
    }

    /// <summary>repair_damage().</summary>
    public void Repair(Entity<BlobStructureComponent> ent, float amount)
    {
        ent.Comp.Integrity = Math.Min(ent.Comp.MaxIntegrity, ent.Comp.Integrity + amount);
        UpdateState(ent, ent.Comp);
    }

    private void OnEmp(Entity<BlobStructureComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        if (ent.Comp.Overmind != null)
            _strain.EmpReaction(ent);

        // prob(100 - severity * 30): тяжёлый ЭМИ.
        if (!TerminatingOrDeleted(ent) && _random.Prob(0.7f))
        {
            var emp = Spawn(EmpEffect, Transform(ent).Coordinates);
            _transform.SetLocalRotation(emp, Angle.FromDegrees(90 * _random.Next(4)));
        }
    }

    /// <summary>extinguish(): вода и другие тушащие жидкости.</summary>
    private void OnReaction(Entity<BlobStructureComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || args.Reagent.ReactiveEffects == null ||
            !args.Reagent.ReactiveEffects.ContainsKey("Extinguish") || ent.Comp.Overmind == null)
            return;

        _strain.ExtinguishReaction(ent);
    }

    #endregion

    #region Внешний вид и осмотр

    public Color GetColor(BlobStructureComponent comp, EntityUid? controller = null)
    {
        var overmind = controller ?? comp.Overmind;
        if (overmind == null || !TryComp<BlobOvermindComponent>(overmind, out var om) ||
            !_proto.TryIndex(om.Strain, out var strain))
            return Color.White;

        // Блоб вне станции светлее.
        return comp.Legit ? strain.Color : Color.InterpolateBetween(strain.Color, Color.White, 0.5f);
    }

    /// <summary>update_appearance(): цвет, состояние повреждения, имя и описание.</summary>
    public void UpdateState(EntityUid uid, BlobStructureComponent comp)
    {
        if (TerminatingOrDeleted(uid))
            return;

        _appearance.SetData(uid, BlobVisuals.Color, GetColor(comp));

        switch (comp.Type)
        {
            case BlobStructureType.Normal:
            {
                var fragile = comp.Integrity <= 15;
                _appearance.SetData(uid, BlobVisuals.State, fragile ? "blob_damaged" : "blob");
                comp.BruteResist = fragile ? 0.5f : 0.25f;
                var name = Loc.GetString(fragile ? "blob-normal-name-fragile" : comp.Overmind == null ? "blob-normal-name-dead" : "blob-normal-name");
                var desc = Loc.GetString(fragile ? "blob-normal-desc-fragile" : comp.Overmind == null ? "blob-normal-desc-dead" : "blob-normal-desc");
                SetNameDesc(uid, name, desc);
                break;
            }
            case BlobStructureType.Strong:
            case BlobStructureType.Reflective:
            {
                var weakened = comp.Integrity < comp.MaxIntegrity * 0.5f;
                _appearance.SetData(uid, BlobVisuals.State, weakened ? comp.BaseState + "_damaged" : comp.BaseState);
                var key = comp.Type == BlobStructureType.Strong ? "blob-strong" : "blob-reflective";
                SetNameDesc(uid,
                    Loc.GetString(weakened ? key + "-name-weakened" : key + "-name"),
                    Loc.GetString(weakened ? key + "-desc-damaged" : key + "-desc"));
                break;
            }
            default:
                _appearance.SetData(uid, BlobVisuals.State, comp.BaseState);
                break;
        }
    }

    private void SetNameDesc(EntityUid uid, string name, string desc)
    {
        var meta = MetaData(uid);
        if (meta.EntityName != name)
            _meta.SetEntityName(uid, name, meta);
        if (meta.EntityDescription != desc)
            _meta.SetEntityDescription(uid, desc, meta);
    }

    private void OnExamined(Entity<BlobStructureComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om) &&
            (args.Examiner == overmind || HasComp<GhostComponent>(args.Examiner)))
        {
            args.PushMarkup(Loc.GetString("blob-examine-progress", ("count", om.BlobsLegit.Count), ("win", om.WinCount)));
        }

        args.PushMarkup(Loc.GetString("blob-examine-made-of", ("chem", GetChemName(ent.Comp))));
    }

    public string GetChemName(BlobStructureComponent comp)
    {
        if (comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om) &&
            _proto.TryIndex(om.Strain, out var strain))
            return Loc.GetString(strain.Name);
        return Loc.GetString("blob-chem-unknown");
    }

    /// <summary>attackby(TOOL_ANALYZER): отчёт анализатора.</summary>
    private void OnInteractUsing(Entity<BlobStructureComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<GasAnalyzerComponent>(args.Used))
            return;
        args.Handled = true;

        _audio.PlayEntity(new SoundPathSpecifier("/Audio/Imperial/blob/ping.ogg"), args.User, args.User);
        var lines = new List<string> { Loc.GetString("blob-analyzer-header") };
        if (ent.Comp.Overmind is { } overmind && TryComp<BlobOvermindComponent>(overmind, out var om) &&
            _proto.TryIndex(om.Strain, out var strain))
        {
            lines.Add(Loc.GetString("blob-examine-progress", ("count", om.BlobsLegit.Count), ("win", om.WinCount)));
            lines.Add(Loc.GetString("blob-analyzer-material", ("color", strain.Color.ToHex()), ("name", Loc.GetString(strain.Name))));
            lines.Add(Loc.GetString("blob-analyzer-effects", ("text", Loc.GetString(strain.AnalyzerDamage))));
            lines.Add(Loc.GetString("blob-analyzer-properties", ("text", strain.AnalyzerEffect is { } eff ? Loc.GetString(eff) : "N/A")));
        }
        else
        {
            lines.Add(Loc.GetString("blob-analyzer-neutralized"));
        }

        lines.Add(Loc.GetString("blob-analyzer-type", ("name", Loc.GetString($"blob-type-{ent.Comp.Type.ToString().ToLowerInvariant()}"))));
        lines.Add(Loc.GetString("blob-analyzer-health", ("health", MathF.Round(ent.Comp.Integrity, 1)), ("max", ent.Comp.MaxIntegrity)));
        lines.Add(Loc.GetString("blob-analyzer-scanner", ("text", ScannerReport(ent.Comp))));

        _overmind.SendMessage(args.User, string.Join("\n", lines));
    }

    /// <summary>scannerreport().</summary>
    private string ScannerReport(BlobStructureComponent comp)
    {
        return comp.Type switch
        {
            BlobStructureType.Normal => Loc.GetString(comp.Integrity <= 15 ? "blob-scanner-normal-weak" : "blob-scanner-na"),
            BlobStructureType.Strong or BlobStructureType.Reflective => Loc.GetString(comp.AtmosBlock ? "blob-scanner-shield" : "blob-scanner-na"),
            BlobStructureType.Core => Loc.GetString("blob-scanner-core"),
            BlobStructureType.Node => Loc.GetString("blob-scanner-node"),
            BlobStructureType.Factory => Loc.GetString(comp.Blobbernaut != null ? "blob-scanner-factory-naut" : "blob-scanner-factory"),
            BlobStructureType.Resource => Loc.GetString("blob-scanner-resource"),
            _ => Loc.GetString("blob-scanner-na"),
        };
    }

    #endregion

    /// <summary>Громкость BYOND (0–100) в децибелы.</summary>
    public static float Db(float volume) => volume >= 100 ? 0f : MathF.Max(-20f, 20f * MathF.Log10(volume / 100f));
}
