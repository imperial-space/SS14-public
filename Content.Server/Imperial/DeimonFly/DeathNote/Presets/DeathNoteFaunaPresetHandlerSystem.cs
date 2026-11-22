using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Administration.Logs;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Pinpointer;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Создаёт штатных враждебных существ рядом с целью и задаёт её как приоритет HTN.
/// </summary>
public sealed class DeathNoteFaunaPresetHandlerSystem : DeathNotePresetHandlerSystem, IDeathNotePresetHandler
{
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Fauna;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeathNotePriorityTargetComponent, MeleeHitEvent>(OnPriorityMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeathNotePriorityTargetComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var priority, out var htn))
        {
            if (Deleted(priority.Target) || _mobState.IsDead(priority.Target))
            {
                RemCompDeferred<DeathNotePriorityTargetComponent>(uid);
                _htn.Replan(htn);
                continue;
            }

            if (_timing.CurTime < priority.NextRetargetAt)
                continue;

            priority.NextRetargetAt = _timing.CurTime + priority.RetargetInterval;
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, priority.Target);
            _npc.WakeNPC(uid, htn);
            _htn.Replan(htn);
        }
    }

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before fauna execution.");

        if (parameters.EntityPrototype is not { } prototypeId ||
            !_prototypeManager.TryIndex(prototypeId, out EntityPrototype? prototype) ||
            !prototype.TryGetComponent<HTNComponent>(out _, EntityManager.ComponentFactory))
        {
            return DeathNotePresetExecutionResult.Failed("Configured fauna prototype is missing or has no HTN component.");
        }

        if (parameters.SpawnCount <= 0 ||
            parameters.MaximumSpawnCount <= 0 ||
            parameters.SpawnCount > parameters.MaximumSpawnCount ||
            parameters.SpawnRadius < 0f ||
            parameters.RiftSpawnRadius < 0f ||
            parameters.RetargetInterval <= TimeSpan.Zero)
        {
            return DeathNotePresetExecutionResult.Failed("Fauna spawn count or radius is outside the supported range.");
        }

        var targetCoordinates = _transform.GetMapCoordinates(context.Target);
        if (targetCoordinates.MapId == MapId.Nullspace)
            return DeathNotePresetExecutionResult.Failed("Target was in nullspace at fauna execution time.");

        EntityUid? rift = null;
        if (parameters.RiftPrototype is { } riftPrototype)
        {
            if (!_prototypeManager.HasIndex<EntityPrototype>(riftPrototype))
                return DeathNotePresetExecutionResult.Failed("Configured fauna rift prototype does not exist.");

            rift = Spawn(riftPrototype, targetCoordinates);
            AnnounceRift(rift.Value, parameters.AnnouncementSound);
        }

        for (var index = 0; index < parameters.SpawnCount; index++)
        {
            var radius = rift != null
                ? Math.Min(parameters.SpawnRadius, parameters.RiftSpawnRadius)
                : parameters.SpawnRadius;
            var coordinates = targetCoordinates.Offset(_random.NextVector2(radius));
            var creature = Spawn(prototypeId, coordinates);
            var priority = EnsureComp<DeathNotePriorityTargetComponent>(creature);
            priority.Target = context.Target;
            priority.RetargetInterval = parameters.RetargetInterval;
            priority.NextRetargetAt = _timing.CurTime + priority.RetargetInterval;
            _npc.SetBlackboard(creature, NPCBlackboard.CurrentOrderedTarget, context.Target);
            if (TryComp(creature, out HTNComponent? htn))
            {
                _npc.WakeNPC(creature, htn);
                _htn.Replan(htn);
            }
        }

        var eventStarted = rift == null && parameters.GameRule is { } gameRule &&
                           _gameTicker.StartGameRule(gameRule.ToString());

        return DeathNotePresetExecutionResult.Succeeded(
            $"Spawned {parameters.SpawnCount} targeted hostile creatures; rift: {rift}; station fauna event started: {eventStarted}.");
    }

    private void AnnounceRift(EntityUid rift, SoundSpecifier? announcementSound)
    {
        var xform = Transform(rift);
        var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString((rift, xform)));
        var message = Loc.GetString("carp-rift-warning", ("location", location));
        _chat.DispatchGlobalAnnouncement(message, playSound: false, colorOverride: Color.Red);
        if (announcementSound != null)
            _audio.PlayGlobal(announcementSound, Filter.Broadcast(), true);
    }

    private void OnPriorityMeleeHit(Entity<DeathNotePriorityTargetComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (hit != ent.Comp.Target)
                continue;

            if (_damageable.TryChangeDamage(
                    hit,
                    args.BaseDamage,
                    out var appliedDamage,
                    ignoreResistances: true,
                    interruptsDoAfters: true,
                    origin: ent.Owner,
                    ignoreGlobalModifiers: true) &&
                appliedDamage.GetTotal() > FixedPoint2.Zero)
            {
                _adminLog.Add(
                    LogType.MeleeHit,
                    LogImpact.Medium,
                    $"{ent.Owner:actor} hit priority Death Note target {hit:subject} and dealt " +
                    $"{appliedDamage.GetTotal():damage} resistance-bypassing damage.");
            }
        }

        // При одиночном ударе штатный пайплайн всё ещё оставляет контактные улики,
        // событие атаки и звук, но не дублирует уже нанесённый гарантированный урон.
        // При размашистой атаке нельзя отменять базовый урон только одной цели,
        // поэтому окружающие и цель дополнительно обрабатываются обычной механикой.
        if (args.HitEntities.Count == 1 && args.HitEntities[0] == ent.Comp.Target)
            args.BonusDamage -= args.BaseDamage;
    }
}
