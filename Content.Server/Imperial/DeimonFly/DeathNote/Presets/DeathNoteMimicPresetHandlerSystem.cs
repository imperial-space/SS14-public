using Content.Server.Administration.Logs;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Storage.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.VendingMachines;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Заменяет ближайший торговый автомат или шкаф-хранилище мимиком с назначенной целью.
/// </summary>
public sealed class DeathNoteMimicPresetHandlerSystem : EntitySystem, IDeathNotePresetHandler
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Mimic;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeathNoteMimicComponent, MeleeHitEvent>(OnMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeathNoteMimicComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var mimic, out var htn))
        {
            if (Deleted(mimic.Target) || _mobState.IsDead(mimic.Target))
            {
                RemCompDeferred<DeathNoteMimicComponent>(uid);
                _htn.Replan(htn);
                continue;
            }

            if (_timing.CurTime < mimic.NextRetargetAt)
                continue;

            mimic.NextRetargetAt = _timing.CurTime + mimic.RetargetInterval;
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, mimic.Target);
            _npc.WakeNPC(uid, htn);
            _htn.Replan(htn);
        }
    }

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target) ||
            parameters.EntityPrototype is not { } mimicPrototype ||
            !_prototypes.TryIndex(mimicPrototype, out EntityPrototype? prototype) ||
            !prototype.TryGetComponent<HTNComponent>(out _, EntityManager.ComponentFactory) ||
            !prototype.TryGetComponent<MeleeWeaponComponent>(out _, EntityManager.ComponentFactory) ||
            parameters.RetargetInterval <= TimeSpan.Zero ||
            parameters.SpawnRadius <= 0f ||
            parameters.MimicMinimumTargetHits <= 0 ||
            parameters.MimicMaximumTargetHits < parameters.MimicMinimumTargetHits ||
            !DeathNoteDamageHelper.HasValidRandomRange(parameters))
        {
            return DeathNotePresetExecutionResult.Failed("Mimic target or prototype is unavailable.");
        }

        var targetCoordinates = _transform.GetMapCoordinates(context.Target);
        if (targetCoordinates.MapId == MapId.Nullspace ||
            !TryFindNearestConvertible(targetCoordinates, parameters.SpawnRadius, out var source))
        {
            return DeathNotePresetExecutionResult.Failed("No vending machine or locker was found near the target.");
        }

        var spawnCoordinates = Transform(source).Coordinates;
        // Сначала создаём и проверяем замену. Ошибка спавна не должна уничтожать
        // исходный шкаф или автомат вместе с его содержимым.
        var mimic = Spawn(mimicPrototype, spawnCoordinates);
        if (TryComp(source, out EntityStorageComponent? storage))
            _entityStorage.EmptyContents(source, storage);
        QueueDel(source);

        var special = EnsureComp<DeathNoteMimicComponent>(mimic);
        special.Target = context.Target;
        special.MinimumTargetDamage = parameters.RandomDamageMin;
        special.MaximumTargetDamage = parameters.RandomDamageMax;
        var minimumHits = parameters.MimicMinimumTargetHits;
        var maximumHits = parameters.MimicMaximumTargetHits;
        special.TargetHitsRemaining = _random.Next(minimumHits, maximumHits + 1);
        special.TargetDamageRemaining = _random.NextFloat(
            Math.Min(special.MinimumTargetDamage, special.MaximumTargetDamage),
            Math.Max(special.MinimumTargetDamage, special.MaximumTargetDamage));
        special.RetargetInterval = parameters.RetargetInterval;
        special.NextRetargetAt = _timing.CurTime + special.RetargetInterval;
        _npc.SetBlackboard(mimic, NPCBlackboard.CurrentOrderedTarget, context.Target);
        if (TryComp(mimic, out HTNComponent? htn))
        {
            _npc.WakeNPC(mimic, htn);
            _htn.Replan(htn);
        }

        return DeathNotePresetExecutionResult.Succeeded("Nearest vending machine or locker became a target-aware mimic.");
    }

    private bool TryFindNearestConvertible(MapCoordinates origin, float range, out EntityUid nearest)
    {
        nearest = default;
        var bestDistance = range * range;

        foreach (var (uid, _) in _lookup.GetEntitiesInRange<VendingMachineComponent>(origin, range))
            Consider(uid, Transform(uid), origin, ref nearest, ref bestDistance);

        foreach (var (uid, _) in _lookup.GetEntitiesInRange<EntityStorageComponent>(origin, range))
            Consider(uid, Transform(uid), origin, ref nearest, ref bestDistance);

        return nearest.IsValid();
    }

    private void Consider(
        EntityUid uid,
        TransformComponent xform,
        MapCoordinates origin,
        ref EntityUid nearest,
        ref float bestDistance)
    {
        if (HasComp<DeathNoteFallingVendingComponent>(uid) ||
            HasComp<DeathNoteFallenVendingVisualComponent>(uid))
        {
            return;
        }

        var coordinates = _transform.GetMapCoordinates((uid, xform));
        if (coordinates.MapId != origin.MapId)
            return;

        var distance = (coordinates.Position - origin.Position).LengthSquared();
        if (distance > bestDistance)
            return;

        bestDistance = distance;
        nearest = uid;
    }

    private void OnMeleeHit(Entity<DeathNoteMimicComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (hit != ent.Comp.Target)
                continue;

            var factor = args.BaseDamage.GetTotal().Float();
            if (factor <= 0f || ent.Comp.TargetHitsRemaining <= 0 || ent.Comp.TargetDamageRemaining <= 0f)
                continue;

            var amount = ent.Comp.TargetDamageRemaining / ent.Comp.TargetHitsRemaining;

            if (_damageable.TryChangeDamage(
                    hit,
                    args.BaseDamage * (amount / factor),
                    out var appliedDamage,
                    ignoreResistances: true,
                    interruptsDoAfters: true,
                    origin: ent.Owner,
                    ignoreGlobalModifiers: true) &&
                appliedDamage.GetTotal() > FixedPoint2.Zero)
            {
                ent.Comp.TargetDamageRemaining = Math.Max(
                    0f,
                    ent.Comp.TargetDamageRemaining - appliedDamage.GetTotal().Float());
                ent.Comp.TargetHitsRemaining--;
                _adminLog.Add(
                    LogType.MeleeHit,
                    LogImpact.Medium,
                    $"{ent.Owner:actor} hit its priority Death Note target {hit:subject} and dealt " +
                    $"{appliedDamage.GetTotal():damage} resistance-bypassing damage.");
            }
        }

        // Не прерываем штатный melee-пайплайн: контактные улики, событие атаки,
        // звук и обычный урон по посторонним должны сохраниться.
        if (args.HitEntities.Count == 1 && args.HitEntities[0] == ent.Comp.Target)
            args.BonusDamage -= args.BaseDamage;
    }
}
