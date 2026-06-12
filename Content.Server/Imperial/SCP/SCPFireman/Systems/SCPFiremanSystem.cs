using System.Numerics;
using Content.Server.Actions;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Damage.Systems;
using Content.Server.Imperial.SCP.SCPFireman.Components;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Fluids.Components;
using Content.Shared.Imperial.SCP.SCPFireman.Events;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.FixedPoint;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.SCP.SCPFireman.Systems;

public sealed class SCPFiremanSystem : EntitySystem
{
    private static readonly ReagentId WaterReagent = new("Water", null);
    private static readonly ReagentId SpaceCleanerReagent = new("SpaceCleaner", null);
    private static readonly ProtoId<DamageTypePrototype> StructuralDamageId = "Structural";
    private static readonly ProtoId<AlertPrototype> FirePointsAlert = "SCPFiremanPoints";

    private const float FireballCost = 50f;
    private const float WhirlCost = 30f;
    private const float MeltCost = 20f;
    private const float TrueFlameCost = 90f;
    private const float StrikeCost = 40f;
    private const float SecondModeRange = 6f;

    private const string WallTag = "Wall";
    private const string WindowTag = "Window";
    private const string GateTag = "Gate";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private readonly List<(EntityUid Owner, TimeSpan TriggerTime)> _pendingStrikes = new();
    private readonly List<(EntityUid Source, EntityUid Owner, int MaxSpread)> _pendingSpreads = new();
    private readonly HashSet<Entity<FlammableComponent>> _nearFlammables = new();
    private readonly HashSet<Entity<PuddleComponent>> _nearPuddles = new();
    private readonly HashSet<Entity<SCPFireSpreadComponent>> _nearFires = new();
    private readonly HashSet<Entity<TransformComponent>> _nearStructures = new();
    private readonly Dictionary<EntityUid, int> _ownedFireCounts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SCPFiremanComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SCPFiremanComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SCPFiremanComponent, SCPFiremanIgniteActionEvent>(OnIgniteAction);
        SubscribeLocalEvent<SCPFiremanComponent, SCPFiremanFireballActionEvent>(OnFireballAction);
        SubscribeLocalEvent<SCPFiremanComponent, SCPFiremanWhirlActionEvent>(OnWhirlAction);
        SubscribeLocalEvent<SCPFiremanComponent, SCPFiremanMeltActionEvent>(OnMeltAction);
        SubscribeLocalEvent<SCPFiremanComponent, SCPFiremanTrueFlameActionEvent>(OnTrueFlameAction);
        SubscribeLocalEvent<SCPFiremanComponent, SCPFiremanStrikeActionEvent>(OnStrikeAction);
        SubscribeLocalEvent<SCPFiremanComponent, SCPFiremanSecondModeActionEvent>(OnSecondModeAction);
        SubscribeLocalEvent<SCPFiremanComponent, UserActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<SCPFireSpreadComponent, MapInitEvent>(OnSpreadMapInit);
        SubscribeLocalEvent<SCPFireSpreadComponent, ExtinguishEvent>(OnSpreadExtinguish);
        SubscribeLocalEvent<SCPFireSpreadComponent, ExtinguishedEvent>(OnSpreadExtinguished);
        SubscribeLocalEvent<SCPFireSpreadComponent, ReactionEntityEvent>(OnSpreadReactionEntity);
        SubscribeLocalEvent<SCPFireSpreadComponent, ComponentShutdown>(OnSpreadShutdown);

        SubscribeLocalEvent<SCPFireWhirlComponent, MapInitEvent>(OnWhirlMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateFiremen();
        UpdateSpreadFires();
        UpdateWhirls(frameTime);
        UpdatePendingStrikes();
    }

    private void OnMapInit(Entity<SCPFiremanComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.IgniteActionEntity, ent.Comp.IgniteAction);
        _actions.AddAction(ent, ref ent.Comp.FireballActionEntity, ent.Comp.FireballAction);
        _actions.AddAction(ent, ref ent.Comp.WhirlActionEntity, ent.Comp.WhirlAction);
        _actions.AddAction(ent, ref ent.Comp.MeltActionEntity, ent.Comp.MeltAction);
        _actions.AddAction(ent, ref ent.Comp.TrueFlameActionEntity, ent.Comp.TrueFlameAction);
        _actions.AddAction(ent, ref ent.Comp.StrikeActionEntity, ent.Comp.StrikeAction);
        _actions.AddAction(ent, ref ent.Comp.SecondModeActionEntity, ent.Comp.SecondModeAction);

        var now = _timing.CurTime;
        ent.Comp.FirePoints = ent.Comp.MaxFirePoints;
        ent.Comp.NextPassiveTick = now + TimeSpan.FromSeconds(1);
        ent.Comp.NextSecondModeDrainTick = now + TimeSpan.FromSeconds(1);
        Dirty(ent.Owner, ent.Comp);
        UpdateFirePointsAlert(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<SCPFiremanComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, FirePointsAlert);
        _godmode.DisableGodmode(ent.Owner);
    }

    private void OnIgniteAction(Entity<SCPFiremanComponent> ent, ref SCPFiremanIgniteActionEvent args)
    {
        if (args.Handled)
            return;

        SpawnFireAtEntity(ent.Owner, ent.Owner);
        args.Handled = true;
    }

    private void OnFireballAction(Entity<SCPFiremanComponent> ent, ref SCPFiremanFireballActionEvent args)
    {
        if (args.Handled || !TryConsumePoints(ent, FireballCost))
            return;

        var target = args.Target.ToMap(EntityManager, _transform);
        SpawnFireAtMap(target, ent.Owner);
        args.Handled = true;
    }

    private void OnWhirlAction(Entity<SCPFiremanComponent> ent, ref SCPFiremanWhirlActionEvent args)
    {
        if (args.Handled || !TryConsumePoints(ent, WhirlCost) || !TryGetMapCoordinates(ent.Owner, out var start))
            return;

        var target = args.Target.ToMap(EntityManager, _transform);
        var direction = target.Position - start.Position;
        if (direction.LengthSquared() <= 0.001f)
            direction = Vector2.UnitX;

        direction = Vector2.Normalize(direction);

        var whirl = Spawn("SCPFiremanWhirl", Transform(ent.Owner).Coordinates);
        if (TryComp<SCPFireWhirlComponent>(whirl, out var whirlComp))
        {
            whirlComp.FireOwner = ent.Owner;
            whirlComp.Direction = direction;
            whirlComp.EndTime = _timing.CurTime + whirlComp.Lifetime;

            var timedDespawn = EnsureComp<TimedDespawnComponent>(whirl);
            timedDespawn.Lifetime = (float) whirlComp.Lifetime.TotalSeconds;
        }

        args.Handled = true;
    }

    private void OnMeltAction(Entity<SCPFiremanComponent> ent, ref SCPFiremanMeltActionEvent args)
    {
        if (args.Handled || !TryConsumePoints(ent, MeltCost))
            return;

        if (!EntityManager.EntityExists(args.Target))
            return;

        if (_prototypes.TryIndex(StructuralDamageId, out DamageTypePrototype? structuralDamage))
        {
            var damage = new DamageSpecifier(structuralDamage, FixedPoint2.New(100));
            _damageable.TryChangeDamage(args.Target, damage, origin: ent.Owner);
        }

        SpawnFireAtEntity(args.Target, ent.Owner);
        args.Handled = true;
    }

    private void OnTrueFlameAction(Entity<SCPFiremanComponent> ent, ref SCPFiremanTrueFlameActionEvent args)
    {
        if (args.Handled || !TryConsumePoints(ent, TrueFlameCost))
            return;

        ent.Comp.TrueFlameActive = true;
        ent.Comp.TrueFlameEnd = _timing.CurTime + ent.Comp.TrueFlameDuration;
        _godmode.EnableGodmode(ent.Owner);
        if (ent.Comp.TrueFlameActionEntity != null)
            _actions.SetCooldown(ent.Comp.TrueFlameActionEntity.Value, _timing.CurTime, _timing.CurTime + ent.Comp.TrueFlameDuration);
        args.Handled = true;
    }

    private void OnStrikeAction(Entity<SCPFiremanComponent> ent, ref SCPFiremanStrikeActionEvent args)
    {
        if (args.Handled || !TryConsumePoints(ent, StrikeCost))
            return;

        _pendingStrikes.Add((ent.Owner, _timing.CurTime + TimeSpan.FromSeconds(1.5f)));
        args.Handled = true;
    }

    private void OnSecondModeAction(Entity<SCPFiremanComponent> ent, ref SCPFiremanSecondModeActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.SecondModeEnabled = !ent.Comp.SecondModeEnabled;
        args.Handled = true;
    }

    private void OnActivateInWorld(Entity<SCPFiremanComponent> ent, ref UserActivateInWorldEvent args)
    {
        if (args.Handled || args.User != ent.Owner || !ent.Comp.SecondModeEnabled)
            return;

        if (!EntityManager.EntityExists(args.Target) || !TryGetMapCoordinates(ent.Owner, out var start) || !TryGetMapCoordinates(args.Target, out var end))
            return;

        if (start.MapId != end.MapId)
            return;

        EmitSecondModeFire(ent.Owner, start, end);
        args.Handled = true;
    }

    private void OnBeforeDamageChanged(Entity<SCPFiremanComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!ent.Comp.TrueFlameActive)
            return;

        if (_timing.CurTime >= ent.Comp.TrueFlameEnd)
            return;

        args.Cancelled = true;
    }

    private void OnSpreadMapInit(Entity<SCPFireSpreadComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        ent.Comp.NextSpreadTime = now + ent.Comp.SpreadDelay;
        ent.Comp.NextPointTime = now + ent.Comp.PointInterval;
        ent.Comp.NextHealTime = now + ent.Comp.HealInterval;
        ent.Comp.NextIgniteTime = now + ent.Comp.IgniteInterval;

        if (ent.Comp.FireOwner != EntityUid.Invalid)
            IncrementOwnedFire(ent.Comp.FireOwner);
    }

    private void OnSpreadShutdown(Entity<SCPFireSpreadComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.FireOwner != EntityUid.Invalid)
            DecrementOwnedFire(ent.Comp.FireOwner);
    }

    private void OnSpreadExtinguished(Entity<SCPFireSpreadComponent> ent, ref ExtinguishedEvent args)
    {
        QueueDel(ent.Owner);
    }

    private void OnSpreadExtinguish(Entity<SCPFireSpreadComponent> ent, ref ExtinguishEvent args)
    {
        QueueDel(ent.Owner);
    }

    private void OnSpreadReactionEntity(Entity<SCPFireSpreadComponent> ent, ref ReactionEntityEvent args)
    {
        var reagentId = args.Reagent.ID;
        if (reagentId != WaterReagent.Prototype && reagentId != SpaceCleanerReagent.Prototype)
            return;

        QueueDel(ent.Owner);
    }

    private void OnWhirlMapInit(Entity<SCPFireWhirlComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.EndTime = _timing.CurTime + ent.Comp.Lifetime;
        ent.Comp.NextEffectTime = _timing.CurTime;

        var timedDespawn = EnsureComp<TimedDespawnComponent>(ent.Owner);
        timedDespawn.Lifetime = (float) ent.Comp.Lifetime.TotalSeconds;
    }

    private void UpdateFiremen()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SCPFiremanComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.TrueFlameActive && now >= comp.TrueFlameEnd)
            {
                comp.TrueFlameActive = false;
                _godmode.DisableGodmode(uid);
            }

            while (now >= comp.NextPassiveTick)
            {
                AddPoints(uid, comp.PassiveRegenPerSecond, comp);
                comp.NextPassiveTick += TimeSpan.FromSeconds(1);
            }

            if (!comp.SecondModeEnabled)
                continue;

            while (now >= comp.NextSecondModeDrainTick)
            {
                if (comp.FirePoints <= 0f)
                {
                    comp.SecondModeEnabled = false;
                    comp.NextSecondModeDrainTick = now + TimeSpan.FromSeconds(1);
                    break;
                }

                AddPoints(uid, -comp.SecondModeDrainPerSecond, comp);
                comp.NextSecondModeDrainTick += TimeSpan.FromSeconds(1);

                if (comp.FirePoints <= 0f)
                {
                    comp.SecondModeEnabled = false;
                    comp.NextSecondModeDrainTick = now + TimeSpan.FromSeconds(1);
                    break;
                }
            }
        }
    }

    private void UpdateSpreadFires()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SCPFireSpreadComponent>();
        _pendingSpreads.Clear();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryGetMapCoordinates(uid, out var mapCoords))
                continue;

            if (IsWaterAt(mapCoords))
            {
                QueueDel(uid);
                continue;
            }

            var hasOwner = comp.FireOwner != EntityUid.Invalid;

            while (now >= comp.NextPointTime)
            {
                if (hasOwner && TryComp<SCPFiremanComponent>(comp.FireOwner, out var fireman))
                    AddPoints(comp.FireOwner, 1f, fireman);

                comp.NextPointTime += comp.PointInterval;
            }

            while (now >= comp.NextHealTime)
            {
                if (hasOwner)
                    HealOwner(comp.FireOwner, comp.HealPerType);

                comp.NextHealTime += comp.HealInterval;
            }

            while (now >= comp.NextIgniteTime)
            {
                IgniteEntitiesInRadius(mapCoords, comp.IgniteRadius, comp.FireOwner);
                comp.NextIgniteTime += comp.IgniteInterval;
            }

            while (now >= comp.NextSpreadTime)
            {
                _pendingSpreads.Add((uid, comp.FireOwner, comp.MaxSpreadPerPulse));
                comp.NextSpreadTime += comp.SpreadInterval;
            }
        }

        foreach (var spread in _pendingSpreads)
        {
            SpreadFire(spread.Source, spread.Owner, spread.MaxSpread);
        }
    }

    private void UpdateWhirls(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SCPFireWhirlComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.EndTime || !TryGetMapCoordinates(uid, out var mapCoords))
            {
                QueueDel(uid);
                continue;
            }

            var nextPosition = mapCoords.Position + comp.Direction * comp.Speed * frameTime;
            var nextCoords = new MapCoordinates(nextPosition, mapCoords.MapId);

            if (IsBlockedBySolidTag(nextCoords))
            {
                comp.Direction = -comp.Direction;
                nextPosition = mapCoords.Position + comp.Direction * comp.Speed * frameTime;
                nextCoords = new MapCoordinates(nextPosition, mapCoords.MapId);
            }

            _transform.SetWorldPosition(uid, nextPosition);

            if (now >= comp.NextEffectTime)
            {
                comp.NextEffectTime = now + comp.EffectInterval;
                IgniteEntitiesInRadius(nextCoords, comp.IgniteRadius, comp.FireOwner);
                SpawnFireAtMap(nextCoords, comp.FireOwner);
            }
        }
    }

    private void UpdatePendingStrikes()
    {
        if (_pendingStrikes.Count == 0)
            return;

        var now = _timing.CurTime;

        for (var i = _pendingStrikes.Count - 1; i >= 0; i--)
        {
            var strike = _pendingStrikes[i];
            if (now < strike.TriggerTime)
                continue;

            _pendingStrikes.RemoveAt(i);

            if (!TryGetMapCoordinates(strike.Owner, out var center))
                continue;

            IgniteEntitiesInRadius(center, 20f, strike.Owner);
            SpawnFireAtMap(center, strike.Owner);
        }
    }

    private bool TryConsumePoints(Entity<SCPFiremanComponent> ent, float cost)
    {
        if (ent.Comp.FirePoints < cost)
        {
            _popup.PopupEntity("Недостаточно огненных очков.", ent, ent, PopupType.MediumCaution);
            return false;
        }

        AddPoints(ent.Owner, -cost, ent.Comp);
        return true;
    }

    private void AddPoints(EntityUid uid, float delta, SCPFiremanComponent comp)
    {
        comp.FirePoints = Math.Clamp(comp.FirePoints + delta, 0f, comp.MaxFirePoints);
        Dirty(uid, comp);
        UpdateFirePointsAlert(uid, comp);
    }

    private void UpdateFirePointsAlert(EntityUid uid, SCPFiremanComponent comp)
    {
        var severity = (short) Math.Clamp((int) MathF.Floor(comp.FirePoints / 10f), 0, 10);
        _alerts.ShowAlert(uid, FirePointsAlert, severity);
    }

    private void HealOwner(EntityUid owner, float perType)
    {
        if (!TryComp<DamageableComponent>(owner, out var damageable))
            return;

        var healing = new DamageSpecifier();
        foreach (var damageType in damageable.Damage.DamageDict.Keys)
        {
            healing.DamageDict[damageType] = FixedPoint2.New(-perType);
        }

        if (!healing.Empty)
            _damageable.TryChangeDamage(owner, healing, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void SpreadFire(EntityUid source, EntityUid owner, int maxSpreadPerPulse)
    {
        if (!TryGetMapCoordinates(source, out var sourceCoords))
            return;

        var spawned = 0;

        var offset = _random.Next(4);
        for (var i = 0; i < 4; i++)
        {
            if (spawned >= maxSpreadPerPulse)
                break;

            var dirIndex = (offset + i) % 4;
            var direction = dirIndex switch
            {
                0 => Vector2.UnitX,
                1 => -Vector2.UnitX,
                2 => Vector2.UnitY,
                _ => -Vector2.UnitY,
            };

            var candidate = new MapCoordinates(sourceCoords.Position + direction, sourceCoords.MapId);

            if (!IsValidFireTile(candidate))
                continue;

            SpawnFireAtMap(candidate, owner);
            spawned++;
        }
    }

    private void EmitSecondModeFire(EntityUid owner, MapCoordinates start, MapCoordinates target)
    {
        var delta = target.Position - start.Position;
        var length = delta.Length();

        if (length <= 0.01f)
            return;

        var direction = Vector2.Normalize(delta);
        var distance = Math.Min(length, SecondModeRange);
        var steps = Math.Max(1, (int) MathF.Ceiling(distance));

        for (var i = 1; i <= steps; i++)
        {
            var position = start.Position + direction * i;
            SpawnFireAtMap(new MapCoordinates(position, start.MapId), owner);
        }
    }

    private void SpawnFireAtEntity(EntityUid target, EntityUid owner)
    {
        if (!TryGetMapCoordinates(target, out var coords))
            return;

        SpawnFireAtMap(coords, owner);
    }

    private void SpawnFireAtMap(MapCoordinates coords, EntityUid owner)
    {
        if (TryComp<SCPFiremanComponent>(owner, out var fireman) && GetOwnedFireCount(owner) >= fireman.MaxOwnedFires)
            return;

        if (!IsValidFireTile(coords))
            return;

        var fire = Spawn("SCPFiremanFlame", coords);
        if (TryComp<SCPFireSpreadComponent>(fire, out var spread))
        {
            spread.FireOwner = owner;
            IncrementOwnedFire(owner);
        }
    }

    private int GetOwnedFireCount(EntityUid owner)
    {
        if (_ownedFireCounts.TryGetValue(owner, out var cached))
            return cached;

        var count = 0;
        var query = EntityQueryEnumerator<SCPFireSpreadComponent>();
        while (query.MoveNext(out _, out var spread))
        {
            if (spread.FireOwner == owner)
                count++;
        }

        _ownedFireCounts[owner] = count;
        return count;
    }

    private void IncrementOwnedFire(EntityUid owner)
    {
        if (owner == EntityUid.Invalid)
            return;

        _ownedFireCounts.TryGetValue(owner, out var count);
        _ownedFireCounts[owner] = count + 1;
    }

    private void DecrementOwnedFire(EntityUid owner)
    {
        if (owner == EntityUid.Invalid)
            return;

        if (!_ownedFireCounts.TryGetValue(owner, out var count))
            return;

        count = Math.Max(0, count - 1);
        if (count == 0)
            _ownedFireCounts.Remove(owner);
        else
            _ownedFireCounts[owner] = count;
    }

    private void IgniteEntitiesInRadius(MapCoordinates center, float radius, EntityUid source)
    {
        _nearFlammables.Clear();
        _lookup.GetEntitiesInRange(center, radius, _nearFlammables, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);

        foreach (var (uid, flammable) in _nearFlammables)
        {
            _flammable.AdjustFireStacks(uid, 1.5f, flammable);
            _flammable.Ignite(uid, source, flammable);
        }
    }

    private bool IsValidFireTile(MapCoordinates coords)
    {
        if (IsBlockedBySolidTag(coords))
            return false;

        if (IsWaterAt(coords))
            return false;

        _nearFires.Clear();
        _lookup.GetEntitiesInRange(coords, 0.3f, _nearFires, LookupFlags.Static | LookupFlags.Sundries);
        if (_nearFires.Count > 0)
            return false;

        return true;
    }

    private bool IsBlockedBySolidTag(MapCoordinates coords)
    {
        _nearStructures.Clear();
        _lookup.GetEntitiesInRange(coords, 0.45f, _nearStructures, LookupFlags.Static | LookupFlags.Sundries);

        foreach (var (uid, xform) in _nearStructures)
        {
            if (!xform.Anchored)
                continue;

            if (_tag.HasTag(uid, WallTag) || _tag.HasTag(uid, WindowTag) || _tag.HasTag(uid, GateTag))
                return true;
        }

        return false;
    }

    private bool IsWaterAt(MapCoordinates coords)
    {
        _nearPuddles.Clear();
        _lookup.GetEntitiesInRange(coords, 2f, _nearPuddles, LookupFlags.Static | LookupFlags.Sundries);

        foreach (var (uid, puddle) in _nearPuddles)
        {
            if (!_solutions.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
                continue;

            if (solution.ContainsReagent(WaterReagent) || solution.ContainsReagent(SpaceCleanerReagent))
                return true;
        }

        return false;
    }

    private bool TryGetMapCoordinates(EntityUid uid, out MapCoordinates mapCoordinates)
    {
        var xform = Transform(uid);
        if (xform.MapUid is null)
        {
            mapCoordinates = default;
            return false;
        }

        mapCoordinates = xform.Coordinates.ToMap(EntityManager, _transform);
        return true;
    }
}
