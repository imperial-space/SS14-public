using Content.Server.Disposal.Unit;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Events;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Server.Wires;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Unit;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.VendingMachines;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Связывает направляемые предписания тетради смерти со штатными взаимодействиями мира.
/// </summary>
public sealed partial class DeathNoteGuidedScenarioSystem : EntitySystem
{
    [Dependency] private readonly SharedAirlockSystem _airlocks = default!;
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DisposalUnitSystem _disposals = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly WiresSystem _wires = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathNoteGuidedScenarioComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<DeathNoteGuidedScenarioComponent, AttemptIngestEvent>(OnAttemptIngest);
        SubscribeLocalEvent<DeathNoteGuidedScenarioComponent, IngestingEvent>(OnFoodIngested);
        SubscribeLocalEvent<DeathNoteGuidedScenarioComponent, DidEquipHandEvent>(OnItemPickedUp);
        SubscribeLocalEvent<DoorComponent, BeforeDoorOpenedEvent>(
            OnDeathNoteAirlockOpening,
            after: new[] { typeof(SharedAirlockSystem), typeof(SharedDoorSystem) });
        SubscribeLocalEvent<DeathNoteDeadlyAirlockComponent, BeforeDoorClosedEvent>(OnDeadlyAirlockClosing);
        SubscribeLocalEvent<DeathNoteDeadlyAirlockComponent, DoorStateChangedEvent>(OnDeadlyAirlockStateChanged);
        SubscribeLocalEvent<DeathNoteDeadlyAirlockComponent, ComponentShutdown>(OnDeadlyAirlockShutdown);
        SubscribeLocalEvent<DeathNoteVendingVictimComponent, UpdateCanMoveEvent>(OnVendingVictimCanMove);
        SubscribeLocalEvent<DeathNoteFallingVendingComponent, ComponentShutdown>(OnFallingVendingShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var guides = EntityQueryEnumerator<DeathNoteGuidedScenarioComponent>();
        while (guides.MoveNext(out var uid, out var guide))
        {
            // Короткий период после дедлайна позволяет таймеру исполнения надёжно
            // прочитать Triggered до удаления компонента.
            if (!guide.PersistentUntilTriggered &&
                _timing.CurTime >= guide.ExpiresAt + guide.CleanupGracePeriod)
            {
                RemCompDeferred<DeathNoteGuidedScenarioComponent>(uid);
            }
        }

        var airlocks = EntityQueryEnumerator<DeathNoteDeadlyAirlockComponent, AirlockComponent, DoorComponent>();
        while (airlocks.MoveNext(out var uid, out var deadly, out var airlock, out var door))
        {
            if (airlock.Safety)
            {
                RestoreAirlock(uid, deadly, door, airlock);
                RemCompDeferred<DeathNoteDeadlyAirlockComponent>(uid);
                continue;
            }

            if (!deadly.LethalImpactApplied &&
                deadly.ForceCloseAt is { } forceCloseAt &&
                _timing.CurTime >= forceCloseAt)
            {
                if (door.State == DoorState.Open)
                {
                    deadly.ForceCloseAt = null;

                    // This is the event's forced state transition. Going through
                    // TryClose would let a late power/bolt/access veto leave the
                    // assigned victim standing in an indefinitely open airlock.
                    _doors.StartClosing(uid, door);
                }
                else if (door.State is DoorState.Closed or DoorState.Welded)
                {
                    deadly.ForceCloseAt = null;
                }
            }
        }

        var victims = EntityQueryEnumerator<DeathNoteDisposalVictimComponent>();
        while (victims.MoveNext(out var uid, out var victim))
            UpdateDisposalVictim(uid, victim);

        var fallingMachines = EntityQueryEnumerator<DeathNoteFallingVendingComponent, TransformComponent>();
        while (fallingMachines.MoveNext(out var uid, out var falling, out var xform))
            UpdateFallingVending(uid, falling, xform);

        var vendingVictims = EntityQueryEnumerator<DeathNoteVendingVictimComponent>();
        while (vendingVictims.MoveNext(out var uid, out var victim))
        {
            if (Deleted(victim.VendingMachine))
                RemCompDeferred<DeathNoteVendingVictimComponent>(uid);
        }
    }

    private void OnInteractionAttempt(
        Entity<DeathNoteGuidedScenarioComponent> ent,
        ref InteractionAttemptEvent args)
    {
        if (args.Cancelled ||
            args.Target is not { } target ||
            IsGuideExpired(ent.Comp))
        {
            return;
        }

        if (ent.Comp.Scenario is DeathNoteGuidedScenarioType.PoisonedFood or
            DeathNoteGuidedScenarioType.PoisonedDrink)
        {
            TryPoisonTouchedConsumable(ent, target);
            return;
        }

        if (ent.Comp.Triggered)
            return;

        if (ent.Comp.Scenario == DeathNoteGuidedScenarioType.VendingMachineCrush &&
            HasComp<VendingMachineComponent>(target) &&
            !HasComp<DeathNoteFallenVendingVisualComponent>(target) &&
            TryStartVendingFall(target, ent, ent.Comp.VendingFallDuration))
        {
            ent.Comp.Triggered = true;
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.Scenario != DeathNoteGuidedScenarioType.DisposalCatastrophe ||
            !TryComp(target, out DisposalUnitComponent? disposal) ||
            !_disposals.CanInsert(target, disposal, ent.Owner))
        {
            return;
        }

        if (!DeathNoteDamageHelper.TryCreate(
                ent.Comp.Damage,
                ent.Comp.MinimumDamage,
                ent.Comp.MaximumDamage,
                _random,
                out var totalDamage) ||
            !TryStartGuidedEffect(ent))
        {
            return;
        }

        _disposals.AfterInsert(target, disposal, ent.Owner, ent.Owner, doInsert: true);
        _disposals.ManualEngage(target, disposal);

        ent.Comp.Triggered = true;
        var victim = EnsureComp<DeathNoteDisposalVictimComponent>(ent.Owner);
        victim.DisposalUnit = target;
        victim.ImpactInterval = ent.Comp.ImpactInterval;
        victim.NextImpact = _timing.CurTime + victim.ImpactInterval;
        victim.ImpactsRemaining = ent.Comp.ImpactCount;
        totalDamage *= 1f / victim.ImpactsRemaining;
        victim.DamagePerImpact = totalDamage;
        victim.ExpiresAt = _timing.CurTime + victim.ImpactInterval * (victim.ImpactsRemaining + 2);
        args.Cancelled = true;
    }

    private bool TryStartGuidedEffect(Entity<DeathNoteGuidedScenarioComponent> ent)
    {
        var args = new DeathNoteGuidedEffectStartEvent(ent.Comp.EntryId);
        RaiseLocalEvent(ent.Owner, ref args);
        return args.Prepared;
    }

    private bool IsGuideExpired(DeathNoteGuidedScenarioComponent guide)
    {
        return !guide.PersistentUntilTriggered && _timing.CurTime >= guide.ExpiresAt;
    }
}
