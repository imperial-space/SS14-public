using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Падение сквозь этажи (/turf/proc/zFall, zImpact, /mob/living/proc/ZImpactDamage).
/// На каждой клетке открытого пространства стоит невидимая метка со StepTrigger: всё, что на неё попало
/// (вошло, бросили и оно приземлилось, заспавнилось), падает этажом ниже, пока не встретит пол.
/// </summary>
public sealed class ZFallSystem : EntitySystem
{
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ZStairsSystem _stairs = default!;
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _timing = default!;

    public const string MarkerPrototype = "ZOpenSpaceMarker";
    private readonly HashSet<EntityUid> _landed = new();
    private readonly List<EntityUid> _voidCandidates = new();
    private TimeSpan _nextVoidCheck;
    private static readonly SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnGridMapInit);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZOpenSpaceComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<ZOpenSpaceComponent, StepTriggeredOnEvent>(OnStep);
        SubscribeLocalEvent<ThrownItemComponent, LandEvent>(OnLand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var ent in _landed)
        {
            if (!TerminatingOrDeleted(ent) && CanFall(ent, out _))
                ZFall(ent);
        }
        _landed.Clear();

        var now = _timing.CurTime;
        if (now < _nextVoidCheck)
            return;
        _nextVoidCheck = now + TimeSpan.FromSeconds(0.15);
        CheckVoid();
    }

    /// <summary>
    /// Пустота над нижним этажом: всё, что оказалось вне сеток (сошло с края пола, выброшено, заспавнено),
    /// падает этажом ниже. Сущности в пустоте — прямые дети карты.
    /// </summary>
    private void CheckVoid()
    {
        _voidCandidates.Clear();
        var maps = EntityQueryEnumerator<ZLevelMapComponent>();
        while (maps.MoveNext(out var mapUid, out var z))
        {
            if (z.Below == null)
                continue;
            var children = Transform(mapUid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (!HasComp<MapGridComponent>(child))
                    _voidCandidates.Add(child);
            }
        }
        foreach (var ent in _voidCandidates)
        {
            if (!TerminatingOrDeleted(ent) && CanFall(ent, out _))
                ZFall(ent);
        }
    }

    // ---------- Метки открытого пространства ----------

    private void OnGridMapInit(Entity<MapGridComponent> grid, ref MapInitEvent args)
    {
        foreach (var tile in _map.GetAllTiles(grid.Owner, grid.Comp))
        {
            if (ZLevelTiles.IsOpenSpace(_tileDefs, tile.Tile))
                EnsureMarker(grid, tile.GridIndices);
        }
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        // В режиме маппинга карта не инициализирована: метки появятся при её запуске.
        if (!_map.IsInitialized(Transform(args.Entity).MapID))
            return;
        foreach (var change in args.Changes)
        {
            var was = ZLevelTiles.IsOpenSpace(_tileDefs, change.OldTile);
            var now = ZLevelTiles.IsOpenSpace(_tileDefs, change.NewTile);
            if (now && !was)
                EnsureMarker(args.Entity, change.GridIndices);
            else if (was && !now)
                RemoveMarker(args.Entity, change.GridIndices);
        }
    }

    private void EnsureMarker(Entity<MapGridComponent> grid, Vector2i indices)
    {
        foreach (var ent in _map.GetAnchoredEntities(grid.Owner, grid.Comp, indices))
        {
            if (HasComp<ZOpenSpaceComponent>(ent))
                return;
        }
        var marker = Spawn(MarkerPrototype, _map.GridTileToLocal(grid.Owner, grid.Comp, indices));
        _xform.AnchorEntity(marker);
    }

    private void RemoveMarker(Entity<MapGridComponent> grid, Vector2i indices)
    {
        foreach (var ent in _map.GetAnchoredEntities(grid.Owner, grid.Comp, indices))
        {
            if (HasComp<ZOpenSpaceComponent>(ent))
                QueueDel(ent);
        }
    }

    // ---------- Падение ----------

    private void OnStepAttempt(Entity<ZOpenSpaceComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = CanFall(args.Tripper, out _);
    }

    private void OnStep(Entity<ZOpenSpaceComponent> ent, ref StepTriggeredOnEvent args)
    {
        ZFall(args.Tripper);
    }

    /// <summary>Брошенное пролетает над пустотой и падает, когда приземлится на неё.</summary>
    private void OnLand(Entity<ThrownItemComponent> ent, ref LandEvent args)
    {
        // Компонент броска снимается после события — падение на следующем тике.
        _landed.Add(ent);
    }

    /// <summary>Кто реально падает: пристёгнутый падает вместе с тем, к чему пристёгнут (relay на buckled).</summary>
    private EntityUid FallingEntity(EntityUid ent)
    {
        if (TryComp<BuckleComponent>(ent, out var buckle) && buckle.BuckledTo is { } strap)
            return strap;
        return ent;
    }

    /// <summary>
    /// can_z_move(DOWN, ZMOVE_FALL_FLAGS): не падают брошенные, летящие, невесомые и закреплённые;
    /// клетку должно быть можно покинуть вниз, а клетку под ней — принять сверху.
    /// </summary>
    public bool CanFall(EntityUid ent, out MapCoordinates target)
    {
        target = MapCoordinates.Nullspace;
        var falling = FallingEntity(ent);
        var xform = Transform(falling);
        if (xform.Anchored || HasComp<ThrownItemComponent>(falling) || HasComp<GhostComponent>(falling) || HasComp<JetpackUserComponent>(falling))
            return false;
        // На полу без гравитации не падают (!has_gravity). Пустота между этажами — не космос, а проём: там падают всегда.
        if (xform.GridUid != null && _gravity.IsWeightless(falling))
            return false;
        // isliving || isobj: любая незакреплённая физическая сущность (мобы, предметы, ящики, стулья).
        if (!TryComp<PhysicsComponent>(falling, out var body) || body.BodyType == BodyType.Static || HasComp<MapGridComponent>(falling))
            return false;

        var here = _z.SnapToTile(_xform.GetMapCoordinates(falling, xform));
        if (_z.GetStep(here, false) is not { } below)
            return false;
        if (!_z.PassOut(here, false) || !_z.PassIn(below, false))
            return false;
        target = new MapCoordinates(_xform.GetMapCoordinates(falling, xform).Position, below.MapId);
        return true;
    }

    /// <summary>zFall + zImpact: падение на этаж ниже, продолжение, если там тоже пустота, и удар о пол.</summary>
    public bool ZFall(EntityUid ent, int levels = 1)
    {
        if (!CanFall(ent, out var target))
            return false;
        var falling = FallingEntity(ent);
        var fromTile = _z.SnapToTile(_xform.GetMapCoordinates(falling));
        var moving = _z.GetZMoveAffected(falling, false);

        _z.ZMove(falling, target, false);
        foreach (var mov in moving)
            _popup.PopupCoordinates(Loc.GetString("zlevel-falls-through", ("name", Name(mov))), _xform.ToCoordinates(fromTile), PopupType.MediumCaution);

        ZImpact(falling, moving, levels);
        return true;
    }

    private void ZImpact(EntityUid falling, List<EntityUid> moving, int levels)
    {
        var here = _z.SnapToTile(_xform.GetMapCoordinates(falling));

        // intercept_zImpact: лестница принимает упавшего с одного этажа без вреда.
        var intercepted = false;
        foreach (var ent in _z.GetAnchored(here))
        {
            if (TryComp<ZStairsComponent>(ent, out var stairs) && _stairs.InterceptFall((ent, stairs), moving, levels))
            {
                intercepted = true;
                break;
            }
        }

        // Под нами снова пустота — летим дальше, удара ещё не было.
        if (!intercepted && ZFall(falling, levels + 1))
            return;

        foreach (var mov in moving)
        {
            if (!intercepted)
                OnZImpact(mov, levels);
        }
    }

    /// <summary>onZImpact + ZImpactDamage: (уровни × 5)^1.5 урона, стоя — поровну на обе ноги; сбивает с ног на 5 с за уровень.</summary>
    private void OnZImpact(EntityUid ent, int levels)
    {
        _popup.PopupEntity(Loc.GetString("zlevel-crash-into-others", ("name", Name(ent))), ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("zlevel-crash-into-self"), ent, ent, PopupType.LargeCaution);
        _audio.PlayPvs(ImpactSound, ent);

        if (!HasComp<MobStateComponent>(ent))
            return;

        var incoming = MathF.Pow(levels * 5f, 1.5f);
        var total = _standing.IsDown(ent) ? incoming : MathF.Round(incoming / 2f) * 2f;
        var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = total } };
        _damage.TryChangeDamage(ent, damage);
        _stun.TryKnockdown(ent, TimeSpan.FromSeconds(levels * 5));
    }
}
