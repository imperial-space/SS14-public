using System.Threading.Tasks;
using Content.Server.Popups;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Fishing;
using Content.Shared.Fishing.Bui;
using Content.Shared.Fishing.Enums;
using Content.Shared.Imperial.Medieval.Fishing.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Stacks;
using Content.Shared.Throwing;
using Content.Shared.Wieldable.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Sprite;
using Content.Shared.Chemistry.EntitySystems;
using Content.Server.Imperial.Medieval.Farmer;
using Content.Server.Construction;
using Robust.Shared.Timing;
using System.Numerics;
using Content.Shared.Coordinates;

namespace Content.Server.Imperial.Medieval.Fishing;

public sealed partial class FishingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scaleVisuals = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly ITimerManager _timer = default!;

    private readonly List<TaskCompletionSource<float>> _minigameUpdateWaiters = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingRodComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FishingRodComponent, EntInsertedIntoContainerMessage>(OnBaitInserted);
        SubscribeLocalEvent<FishingRodComponent, EntRemovedFromContainerMessage>(OnBaitRemoved);
        SubscribeLocalEvent<FishingRodComponent, FishingDoAfterEvent>(OnFishingDoAfter);
        SubscribeLocalEvent<FishingRodComponent, FishingWaitDoAfterEvent>(OnFishingWaitDoAfter);
        SubscribeLocalEvent<FishingRodComponent, FishingMinigameResultMessage>(OnFishingMinigameResult);
        SubscribeLocalEvent<FishingRodComponent, BoundUIClosedEvent>(OnRodUiClosed);
        SubscribeLocalEvent<HandsComponent, DamageChangedEvent>(OnHandsDamageChanged);

        SubscribeLocalEvent<FishSizeRarityComponent, ConstructionChangeEntityEvent>(OnFishTransformed);
        SubscribeLocalEvent<FishSizeRarityComponent, ScaleEntityEvent>(OnScaleEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_minigameUpdateWaiters.Count == 0)
            return;

        var waiters = _minigameUpdateWaiters.ToArray();
        _minigameUpdateWaiters.Clear();

        foreach (var waiter in waiters)
        {
            waiter.TrySetResult(frameTime);
        }
    }

    private void OnAfterInteract(Entity<FishingRodComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || ent.Comp.MinigameActive)
            return;

        if (!TryComp<WieldableComponent>(ent, out var wieldable) || !wieldable.Wielded)
            return;

        if (!TryGetFishingTarget(ent.Comp, args.Target, args.ClickLocation, out var fishingTarget, out var location))
            return;

        if (fishingTarget is not { } fishingTargetUid)
            return;

        if (!_interaction.InRangeAndAccessible(args.User, fishingTargetUid, ent.Comp.AfterInteractDistanceThreshold))
            return;

        if (ent.Comp.Bait is not { } baitUid || !TryComp<FishingBaitComponent>(baitUid, out _))
        {
            if (ent.Comp.Bait != null)
            {
                ent.Comp.Bait = null;
                Dirty(ent);
            }

            _popup.PopupEntity(Loc.GetString("fishing-no-bait-popup"), args.User, args.User);
            return;
        }

        var fishingEvent = new FishingDoAfterEvent(GetNetCoordinates(args.ClickLocation));
        var initialDelay = Math.Max(0.01f, ent.Comp.InitialDoAfterTime);
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, initialDelay, fishingEvent, ent.Owner, target: fishingTargetUid, used: ent.Owner)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = ent.Comp.DoAfterDistanceThreshold,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameEvent,
            CancelDuplicate = true,
            BlockDuplicate = false,
        };

        args.Handled = true;
        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        StopFishing(ent);

        ent.Comp.LastClickedWater = location;
        Dirty(ent);
    }

    private void StopFishing(Entity<FishingRodComponent> rod)
    {
        if (rod.Comp.MinigameActive) // minigame is stopped via other means
            return;

        var removedBobber = DeleteOwnedBobber(rod);
        if (!removedBobber && rod.Comp.CurrentFish == null)
            return;

        rod.Comp.CurrentFish = null;
        Dirty(rod);
    }

    private bool DeleteOwnedBobber(Entity<FishingRodComponent> rod)
    {
        if (rod.Comp.CurrentBobber is { } trackedBobber)
        {
            rod.Comp.CurrentBobber = null;
            Dirty(rod);

            if (IsOwnedBobber(rod, trackedBobber))
            {
                QueueDel(trackedBobber);
                return true;
            }
        }

        // Fallback for if we missed current bobber
        var query = EntityQueryEnumerator<BobberComponent>();
        while (query.MoveNext(out var bobberUid, out var bobber))
        {
            if (bobber.Rod != rod.Owner)
                continue;

            QueueDel(bobberUid);
            return true;
        }

        return false;
    }

    private void OnBaitInserted(Entity<FishingRodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        const string baitSlotId = "bait_slot";
        if (args.Container.ID != baitSlotId)
            return;

        ent.Comp.Bait = args.Entity;
        Dirty(ent);
    }

    private void OnBaitRemoved(Entity<FishingRodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        const string baitSlotId = "bait_slot";
        if (args.Container.ID != baitSlotId)
            return;

        if (ent.Comp.Bait != args.Entity)
            return;

        ent.Comp.Bait = null;
        Dirty(ent);

        if (ent.Comp.MinigameActive)
        {
            ExitFishingMinigame(ent, consumeBait: false);
            return;
        }

        StopFishing(ent);
    }

    private void OnFishingDoAfter(Entity<FishingRodComponent> ent, ref FishingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var bobberCoordinates = GetCoordinates(args.ClickLocation);
        if (!bobberCoordinates.IsValid(EntityManager))
            return;

        if (!TryPickCurrentFish(ent, args.User))
            return;

        var bobber = Spawn(ent.Comp.BobberPrototype, bobberCoordinates);
        if (TryComp<BobberComponent>(bobber, out var bobberComp))
        {
            bobberComp.Rod = ent.Owner;
            Dirty(bobber, bobberComp);
        }

        _appearance.SetData(bobber, BobberVisuals.State, BobberVisualState.Icon);

        ent.Comp.CurrentBobber = bobber;
        Dirty(ent);

        _audio.PlayPvs(ent.Comp.CastFloatSplashSound, bobberCoordinates);
        StartLoopDoAfter(ent, args.User, bobber);
    }

    private void OnFishingWaitDoAfter(Entity<FishingRodComponent> ent, ref FishingWaitDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var bobber = GetEntity(args.Bobber);
        if (args.Cancelled)
        {
            if (bobber is { } bobberUid && IsOwnedBobber(ent, bobberUid))
                QueueDel(bobberUid);

            ent.Comp.CurrentBobber = null;
            ent.Comp.CurrentFish = null;
            Dirty(ent);
            return;
        }

        if (bobber is not { } bobberUidActive || !IsOwnedBobber(ent, bobberUidActive))
        {
            ent.Comp.CurrentBobber = null;
            ent.Comp.CurrentFish = null;
            Dirty(ent);
            return;
        }

        var baseChance = Math.Max(1, ent.Comp.BaseFishingChancePercent);
        var maxChance = Math.Max(baseChance, ent.Comp.MaxChance);
        var currentChance = Math.Clamp(args.CurrentChance, baseChance, maxChance);

        if (_random.Prob(currentChance / 100f))
        {
            StartFishMinigame(ent, args.User, bobberUidActive);
            return;
        }

        var increment = Math.Max(0, ent.Comp.IncrementChance);
        args.CurrentChance = currentChance < maxChance
            ? Math.Min(maxChance, currentChance + increment)
            : currentChance;

        args.Repeat = true;
    }

    private void OnHandsDamageChanged(EntityUid uid, HandsComponent hands, DamageChangedEvent args)
    {
        if (!args.InterruptsDoAfters || !args.DamageIncreased || args.DamageDelta == null)
            return;

        if (args.DamageDelta.GetTotal() <= 0)
            return;

        if (!_hands.TryGetActiveItem((uid, hands), out var activeItem) || activeItem == null)
            return;

        var rodUid = activeItem.Value;
        if (!TryComp<FishingRodComponent>(rodUid, out var rod))
            return;

        if (!rod.MinigameActive || rod.MinigameUser != uid)
            return;

        ExitFishingMinigame((rodUid, rod));
    }

    private bool CanContinueMinigame(Entity<FishingRodComponent> ent)
    {
        if (!ent.Comp.MinigameActive || ent.Comp.CurrentFish is not { })
            return false;

        if (ent.Comp.MinigameUser is not { } userUid || !Exists(userUid))
            return false;

        if (ent.Comp.CurrentBobber is not { } bobberUid || !Exists(bobberUid))
            return false;

        if (ent.Comp.Bait is not { } baitUid || !Exists(baitUid))
            return false;

        if (!TryComp<WieldableComponent>(ent, out var wieldable) || !wieldable.Wielded)
            return false;

        if (!TryComp<HandsComponent>(userUid, out var hands))
            return false;

        if (!_hands.IsHolding((userUid, hands), ent.Owner))
            return false;

        if (!_hands.TryGetActiveItem((userUid, hands), out var activeItem) || activeItem != ent.Owner)
            return false;

        if (!_transform.InRange(Transform(userUid).Coordinates, ent.Comp.MinigameStartPosition, ent.Comp.MinigameMovementThreshold))
            return false;

        return true;
    }

    private bool TryGetFishSettings(EntProtoId fishPrototypeId, out FishComponent fishSettings)
    {
        fishSettings = default!;

        if (!_prototype.TryIndex<EntityPrototype>(fishPrototypeId, out var fishPrototype))
            return false;

        if (!fishPrototype.TryGetComponent<FishComponent>(out var fishComponent, EntityManager.ComponentFactory) ||
            fishComponent == null)
        {
            return false;
        }

        fishSettings = fishComponent;
        return true;
    }

    private void OnFishingMinigameResult(Entity<FishingRodComponent> ent, ref FishingMinigameResultMessage args)
    {
        if (!ent.Comp.MinigameActive || ent.Comp.MinigameUser != args.Actor)
            return;

        if (!CanContinueMinigame(ent))
        {
            ExitFishingMinigame(ent);
            return;
        }

        if (args.Result == FishingMinigameResult.Complete)
        {
            CompleteFishingMinigame(ent);
            return;
        }

        ExitFishingMinigame(ent);
    }

    private void OnRodUiClosed(Entity<FishingRodComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not FishingRodUiKey.Key)
            return;

        if (!ent.Comp.MinigameActive || ent.Comp.MinigameUser != args.Actor)
            return;

        ExitFishingMinigame(ent);
    }

    private void StartLoopDoAfter(Entity<FishingRodComponent> rod, EntityUid userUid, EntityUid bobberUid)
    {
        var baseChance = Math.Max(1, rod.Comp.BaseFishingChancePercent);
        var loopDelay = Math.Max(0.01f, rod.Comp.LoopDoAfterTime);
        var waitEvent = new FishingWaitDoAfterEvent(GetNetEntity(bobberUid), baseChance);
        var doAfterArgs = new DoAfterArgs(EntityManager, userUid, loopDelay, waitEvent, rod.Owner, target: bobberUid, used: rod.Owner)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = rod.Comp.DoAfterDistanceThreshold,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            rod.Comp.CurrentBobber = null;
            Dirty(rod);
            QueueDel(bobberUid);
        }
    }

    private void StartFishMinigame(Entity<FishingRodComponent> rod, EntityUid userUid, EntityUid bobberUid)
    {
        if (!IsOwnedBobber(rod, bobberUid))
        {
            rod.Comp.CurrentBobber = null;
            rod.Comp.CurrentFish = null;
            Dirty(rod);
            return;
        }

        if (!_ui.HasUi(rod.Owner, FishingRodUiKey.Key) || rod.Comp.CurrentFish is not { } currentFish)
        {
            if (IsOwnedBobber(rod, bobberUid))
                QueueDel(bobberUid);

            rod.Comp.CurrentBobber = null;
            rod.Comp.CurrentFish = null;
            Dirty(rod);
            return;
        }

        if (!TryGetFishSettings(currentFish, out var fishSettings))
        {
            ResetMinigameState(rod, deleteBobber: true, consumeBait: false, clearCurrentFish: true);
            return;
        }

        if (rod.Comp.MinigameActive)
            ResetMinigameState(rod, deleteBobber: true, consumeBait: false, clearCurrentFish: false);

        rod.Comp.Tension = 50f;
        rod.Comp.Progress = 0f;
        rod.Comp.TensionAcceleration = 0f;
        rod.Comp.IsHoldingLmb = false;
        rod.Comp.MinigameActive = true;
        rod.Comp.MinigameUser = userUid;
        rod.Comp.CurrentBobber = bobberUid;
        rod.Comp.MinigameStartPosition = Transform(userUid).Coordinates;
        Dirty(rod);

        _appearance.SetData(bobberUid, BobberVisuals.State, BobberVisualState.Biting);
        _audio.PlayPvs(rod.Comp.MinigameBitePullSound, bobberUid);
        _ui.OpenUi(rod.Owner, FishingRodUiKey.Key, userUid);
        UpdateMinigameUiState(rod, fishSettings);
        _ = RunMinigameMonitorLoop(rod.Owner, rod.Comp);
    }

    private async Task RunMinigameMonitorLoop(EntityUid rodUid, FishingRodComponent rod)
    {
        while (true)
        {
            await WaitForMinigameUpdateAsync();

            if (!HasComp<FishingRodComponent>(rodUid) || !rod.MinigameActive)
                return;

            if (CanContinueMinigame((rodUid, rod)))
                continue;

            NotifyClientStopMinigame((rodUid, rod));
            ExitFishingMinigame((rodUid, rod));
            return;
        }
    }

    private Task<float> WaitForMinigameUpdateAsync()
    {
        var waiter = new TaskCompletionSource<float>();
        _minigameUpdateWaiters.Add(waiter);
        return waiter.Task;
    }

    private void UpdateMinigameUiState(Entity<FishingRodComponent> rod, FishComponent fishSettings)
    {
        NetEntity? bobber = rod.Comp.CurrentBobber is { } bobberUid ? GetNetEntity(bobberUid) : null;
        var state = new FishingMinigameBoundUserInterfaceState(
            rod.Comp.Tension,
            rod.Comp.Progress,
            rod.Comp.TensionAcceleration,
            rod.Comp.BaseAsyncTImeStep,
            fishSettings.TensionAccelerationDelta,
            fishSettings.TensionAccelerationDeltaPressed,
            fishSettings.ProgressPerTick,
            bobber,
            rod.Comp.MinigameActive);
        _ui.SetUiState(rod.Owner, FishingRodUiKey.Key, state);
    }

    private void ExitFishingMinigame(Entity<FishingRodComponent> rod, bool consumeBait = true)
    {
        var minigameUser = rod.Comp.MinigameUser;
        ResetMinigameState(rod, deleteBobber: true, consumeBait: consumeBait, clearCurrentFish: true);

        if (minigameUser is { } userUid)
            _popup.PopupEntity(Loc.GetString("fishing-minigame-failed-popup"), userUid, userUid);
    }

    private void NotifyClientStopMinigame(Entity<FishingRodComponent> rod)
    {
        if (rod.Comp.MinigameUser is not { } userUid)
            return;

        _ui.ServerSendUiMessage(rod.Owner, FishingRodUiKey.Key, new FishingMinigameStopMessage(), userUid);
    }

    private void CompleteFishingMinigame(Entity<FishingRodComponent> rod)
    {
        if (rod.Comp.CurrentFish is not { } currentFish)
        {
            ExitFishingMinigame(rod);
            return;
        }

        var minigameUser = rod.Comp.MinigameUser;
        var bobber = rod.Comp.CurrentBobber;

        var fishName = currentFish.ToString();
        if (_prototype.TryIndex<EntityPrototype>(currentFish, out var fishPrototype))
            fishName = fishPrototype.Name;

        var spawnCoordinates = bobber is { } bobberUid && Exists(bobberUid)
            ? Transform(bobberUid).Coordinates
            : Transform(rod.Owner).Coordinates;

        var fish = Spawn(currentFish, spawnCoordinates);
        GetFishRandomSizeRarity(fish);

        _audio.PlayPvs(rod.Comp.MinigameFishOutSound, spawnCoordinates);
        if (minigameUser is { } userUid && Exists(userUid))
        {
            _popup.PopupEntity(Loc.GetString("fishing-minigame-caught-popup", ("fish", fishName)), userUid, userUid);

            var throwSpeed = GetFishThrowSpeed(fish);
            _throwing.TryThrow(fish, Transform(userUid).Coordinates, throwSpeed, userUid, compensateFriction: true, animated: false);
        }

        ResetMinigameState(rod, deleteBobber: true, consumeBait: true, clearCurrentFish: true);
    }

    private void ResetMinigameState(
        Entity<FishingRodComponent> rod,
        bool deleteBobber,
        bool consumeBait,
        bool clearCurrentFish)
    {
        var user = rod.Comp.MinigameUser;
        var bobber = rod.Comp.CurrentBobber;

        rod.Comp.MinigameActive = false;
        rod.Comp.MinigameUser = null;
        rod.Comp.CurrentBobber = null;
        rod.Comp.MinigameStartPosition = EntityCoordinates.Invalid;
        rod.Comp.IsHoldingLmb = false;
        rod.Comp.Tension = 0f;
        rod.Comp.Progress = 0f;
        rod.Comp.TensionAcceleration = 0f;

        if (clearCurrentFish)
            rod.Comp.CurrentFish = null;

        Dirty(rod);
        _ui.SetUiState(rod.Owner, FishingRodUiKey.Key, null);

        if (user is { } userUid)
            _ui.CloseUi(rod.Owner, FishingRodUiKey.Key, userUid);

        if (deleteBobber && bobber is { } bobberUid && IsOwnedBobber(rod, bobberUid))
            QueueDel(bobberUid);

        if (consumeBait)
            ConsumeBait(rod);
    }

    private void ConsumeBait(Entity<FishingRodComponent> rod)
    {
        if (rod.Comp.Bait is not { } baitUid)
            return;

        if (TryComp<StackComponent>(baitUid, out var baitStack) && baitStack.Count != 1)
        {
            _stack.Use(baitUid, 1, baitStack);
            return;
        }

        rod.Comp.Bait = null;
        Dirty(rod);

        if (Exists(baitUid))
            QueueDel(baitUid);
    }

    private bool TryPickCurrentFish(Entity<FishingRodComponent> rod, EntityUid userUid)
    {
        var availableFish = CollectAvailableFish(rod);
        if (availableFish.Count == 0)
        {
            rod.Comp.CurrentFish = null;
            Dirty(rod);
            _popup.PopupEntity(Loc.GetString("fishing-no-bait-popup"), userUid, userUid);
            return false;
        }

        rod.Comp.CurrentFish = PickWeightedFishPrototype(availableFish);
        Dirty(rod);
        return true;
    }

    private List<(EntProtoId Prototype, float Weight)> CollectAvailableFish(Entity<FishingRodComponent> rod)
    {
        var result = new List<(EntProtoId Prototype, float Weight)>();

        if (rod.Comp.Bait is not { } baitUid || !TryComp<FishingBaitComponent>(baitUid, out var baitComp))
            return result;

        foreach (var (prototypeId, weight) in rod.Comp.FishWeights)
        {
            if (weight <= 0f)
                continue;

            if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var fishPrototype))
                continue;

            if (!fishPrototype.TryGetComponent<FishComponent>(out var fishComponent, EntityManager.ComponentFactory))
                continue;

            if (fishComponent.Location != rod.Comp.LastClickedWater)
                continue;

            if (fishComponent.Level > rod.Comp.Level)
                continue;

            if (fishComponent.Bait != baitComp.BaitType)
                continue;

            result.Add((prototypeId, weight));
        }

        return result;
    }

    private EntProtoId PickWeightedFishPrototype(IReadOnlyList<(EntProtoId Prototype, float Weight)> availableFish)
    {
        var totalWeight = 0f;
        foreach (var (_, weight) in availableFish)
        {
            totalWeight += MathF.Max(0f, weight);
        }

        if (totalWeight <= 0f)
            return availableFish[_random.Next(availableFish.Count)].Prototype;

        var roll = _random.NextFloat(totalWeight);
        var cumulative = 0f;

        foreach (var (prototype, weight) in availableFish)
        {
            cumulative += MathF.Max(0f, weight);
            if (roll <= cumulative)
                return prototype;
        }

        return availableFish[^1].Prototype;
    }

    private float GetFishThrowSpeed(EntityUid fishUid)
    {
        var weightComp = CompOrNull<RDWeightComponent>(fishUid);
        var weight = MathF.Max(0.01f, _rdWeight.GetTotal((fishUid, weightComp)));

        return Math.Clamp(6f / (0.5f + weight), 2f, 6f);
    }

    private bool TryGetFishingTarget(
        FishingRodComponent rod,
        EntityUid? target,
        EntityCoordinates clickLocation,
        out EntityUid? resolvedTarget,
        out FishingLocationType location)
    {
        if (target is { } targetUid && TryGetFishingLocationByPrototype(rod, targetUid, out location))
        {
            resolvedTarget = targetUid;
            return true;
        }

        foreach (var uid in _turf.GetEntitiesInTile(clickLocation))
        {
            if (!TryGetFishingLocationByPrototype(rod, uid, out location))
                continue;

            resolvedTarget = uid;
            return true;
        }

        if (!_turf.TryGetTileRef(clickLocation, out var tileRef) || _turf.IsSpace(tileRef.Value))
        {
            resolvedTarget = rod.Owner;
            location = FishingLocationType.Sea;
            return true;
        }

        resolvedTarget = null;
        location = default;
        return false;
    }

    private bool TryGetFishingLocationByPrototype(FishingRodComponent rod, EntityUid target, out FishingLocationType location)
    {
        var prototypeId = MetaData(target).EntityPrototype?.ID;
        if (prototypeId == null)
        {
            location = default;
            return false;
        }

        switch (prototypeId)
        {
            case var id when rod.SeaWaterPrototype.Equals(id):
                location = FishingLocationType.Sea;
                return true;
            case var id when rod.RiverWaterPrototype.Equals(id) || rod.RiverWaterNoSoundPrototype.Equals(id):
                location = FishingLocationType.River;
                return true;
            default:
                location = default;
                return false;
        }
    }

    private bool IsOwnedBobber(Entity<FishingRodComponent> rod, EntityUid bobberUid)
    {
        if (!Exists(bobberUid))
            return false;

        return TryComp<BobberComponent>(bobberUid, out var bobber) && bobber.Rod == rod.Owner;
    }
}
