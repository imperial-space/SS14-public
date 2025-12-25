using System.Numerics;
using Content.Shared.Imperial.Medieval.SmithingSystem;
using Content.Shared.Imperial.Medieval.SmithingSystem.Bui;
using Content.Shared.Imperial.Medieval.SmithingSystem.Events;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.SmithingSystem;

public sealed partial class SmithingSystem : SharedSmithingSystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeBehaviors();

        SubscribeLocalEvent<SmithingWorkplaceComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<SmithingWorkplaceComponent, EntRemovedFromContainerMessage>(OnSmithEntRemoved);
        SubscribeLocalEvent<SmithingWorkplaceComponent, InteractUsingEvent>(OnInteractUsingEvent);
        SubscribeLocalEvent<SmithingWorkpieceComponent, SmithingCompleteEvent>(OnSmithingComplete);
        SubscribeLocalEvent<SmithingWorkpieceComponent, PullAttemptEvent>(OnPullAttempt);

        Subs.BuiEvents<SmithingWorkplaceComponent>(SmithUiKey.Key,
            subscriber =>
            {
                subscriber.Event<SmithGameEnded>(OnGameFinished);
                subscriber.Event<BoundUIClosedEvent>(OnBuiClosed);
                subscriber.Event<ClientStartedGameEvent>(OnClientStartedGame);
            });
    }

    private void OnPullAttempt(Entity<SmithingWorkpieceComponent> ent, ref PullAttemptEvent args)
    {
        if (ent.Comp.ReadyToForge)
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        FurnanceUpdate();

        var query = EntityQueryEnumerator<SmithingWorkplaceComponent>();

        while (query.MoveNext(out var uid, out var workplaceComponent))
        {
            if (workplaceComponent.GameState == null ||
                !workplaceComponent.GameState.Started ||
                workplaceComponent.GameState.ForceEndTime > _gameTiming.CurTime)
                continue;

            EndGame((uid, workplaceComponent));
        }
    }

    protected override void OnSmithHit(Entity<SmithingWorkplaceComponent> ent, ref SmithHitMesage args)
    {
        base.OnSmithHit(ent, ref args);

        if (!TryComp<SmithingWorkplaceComponent>(ent, out var comp))
            return;

        var randOffsetX = _random.NextFloat();
        var offset = new Vector2(-0.5f + randOffsetX, 0.08f);
        var sparkleCoords = Transform(ent).Coordinates.Offset(offset);
        var effect = Spawn(comp.EffectProto, sparkleCoords);
        _transform.SetParent(effect, ent);

        var state = comp.GameState;
        if (state == null || !state.Started)
            return;

        state.AddStep(args.State, args.Increment);
        if (state.CompletedSteps >= state.StepsTotal)
            EndGame(ent);
    }

    private void OnClientStartedGame(Entity<SmithingWorkplaceComponent> ent, ref ClientStartedGameEvent args)
    {
        ent.Comp.GameState?.Start(_gameTiming.CurTime);
    }

    private void OnBuiClosed(Entity<SmithingWorkplaceComponent> ent, ref BoundUIClosedEvent args)
    {
        if (ent.Comp.GameState is { Started: true })
            EndGame(ent);
    }

    private void EndGame(Entity<SmithingWorkplaceComponent> ent)
    {
        if (ent.Comp.GameState == null || !ent.Comp.Workpiece.HasValue)
            return;

        var state = ent.Comp.GameState;

        var score = state.CalculateScore();

        ent.Comp.GameState = null;

        _itemSlots.SetLock(ent, ent.Comp.WorkpieceSlot, false);

        var ev = new SmithingCompleteEvent(score);
        RaiseLocalEvent(ent.Comp.Workpiece.Value, ref ev);

        _ui.CloseUis(ent.Owner);
    }

    private void OnSmithingComplete(Entity<SmithingWorkpieceComponent> ent, ref SmithingCompleteEvent args)
    {
        var xform = Transform(ent);
        var itemUid = Spawn(ent.Comp.FinalProductEntity, xform.Coordinates);

        var ev = new SmithingApplyBehaviorsEvent(itemUid, args.Score);
        RaiseLocalEvent(ent, ref ev);

        QueueDel(ent);
    }

    private void OnGameFinished(Entity<SmithingWorkplaceComponent> ent, ref SmithGameEnded args)
    {
        if (ent.Comp.GameState is not { } state || !state.Started)
            return;

        if (state.CompletedSteps >= state.StepsTotal)
        {
            EndGame(ent);
            return;
        }

        const double graceSeconds = 0.35;
        var newForceEnd = _gameTiming.CurTime + TimeSpan.FromSeconds(graceSeconds);

        if (state.ForceEndTime > newForceEnd)
            state.ForceEndTime = newForceEnd;
    }


    private void OnEntInserted(Entity<SmithingWorkplaceComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<SmithingWorkpieceComponent>(args.Entity, out var workpiece))
            return;

        ent.Comp.Workpiece = (args.Entity, workpiece);

        var gameDataMessage = GenerateGameData(workpiece);

        ent.Comp.GameState = new SmithGameState(workpiece.Steps, gameDataMessage.CalculateTotalTime());

        _ui.SetUiState(ent.Owner, SmithUiKey.Key, gameDataMessage);
    }

    private SmithGameData GenerateGameData(SmithingWorkpieceComponent workpieceComponent)
    {
        var steps = new Stack<SmithStepData>();

        for (var i = 0; i < workpieceComponent.Steps; i++)
        {
            var stepData = new SmithStepData
            {
                State = SmithHitState.Missed,
                PerfectHitTime = workpieceComponent.ExcellentTime,
                GoodHitTime = workpieceComponent.NothingTime,
            };

            steps.Push(stepData);
        }

        var gameData = new SmithGameData
        {
            SpawnTime = workpieceComponent.StepsSpawnSpeed,
            Steps = steps,
            ItemProtoId = workpieceComponent.FinalProductEntity,
        };

        return gameData;
    }

    private void OnInteractUsingEvent(Entity<SmithingWorkplaceComponent> ent, ref InteractUsingEvent args)
    {
        if (!_tagSystem.HasTag(args.Used, ent.Comp.SmithingToolTag) ||
            !ent.Comp.WorkpieceSlot.HasItem ||
            _ui.IsUiOpen(ent.Owner, SmithUiKey.Key))
            return;

        _itemSlots.SetLock(ent, ent.Comp.WorkpieceSlot, true);
        _ui.OpenUi(ent.Owner, SmithUiKey.Key, args.User);
    }

    private void OnSmithEntRemoved(Entity<SmithingWorkplaceComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        ent.Comp.Workpiece = null;
    }
}
