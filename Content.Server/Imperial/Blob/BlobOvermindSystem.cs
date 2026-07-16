using System.Linq;
using System.Numerics;
using Content.Server.Actions;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Imperial.Blob.Components;
using Content.Server.Hands.Systems;
using Content.Server.Atmos.Components;
using Content.Server.Disposal.Tube;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Radio;
using Content.Server.UserInterface;
using Content.Shared.Alert;
using Content.Shared.Atmos.Components;
using Content.Shared.Chat;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Stunnable;
using Content.Shared.SubFloor;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Blob;

public sealed class BlobOvermindSystem : EntitySystem
{
    private const string BlobFactionId = "Blob";
    private const string BlobAttackSound = "/Audio/Imperial/blob/sound_effects_attackblob.ogg";
    private const string BlobGrowSound = "/Audio/Imperial/blob/sound_effects_splat.ogg";
    private const string BlobMutateSound = "/Audio/Imperial/blob/sound_magic_mutate.ogg";
    private static readonly ProtoId<TagPrototype> CatwalkTag = "Catwalk";
    private static readonly string[] IgnoredInfrastructurePrototypeTokens =
    {
        "Catwalk",
        "Cable",
        "GasPipe",
        "DisposalPipe",
    };

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlobChemistrySystem _chemistry = default!;
    [Dependency] private readonly BlobInfectionSystem _infection = default!;
    [Dependency] private readonly BlobMobSystem _blobMob = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedVisibilitySystem _visibility = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobOvermindComponent, BlobAttackActionEvent>(OnAttackAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobConsumeTileActionEvent>(OnConsumeAction);
        SubscribeLocalEvent<BlobOvermindComponent, EntitySpokeEvent>(OnBlobOvermindSpoke);
        SubscribeLocalEvent<BlobOvermindComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawnerUsed);
        SubscribeLocalEvent<BlobOvermindComponent, BlobUpgradeGenerationActionEvent>(OnUpgradeGenerationAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobUpgradeAttackActionEvent>(OnUpgradeAttackAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobUpgradeCapacityActionEvent>(OnUpgradeCapacityAction);
        SubscribeLocalEvent<BlobOvermindComponent, GetVisMaskEvent>(OnBlobOvermindGetVis);
        SubscribeLocalEvent<BlobOvermindComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlobOvermindComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPlaceTileActionEvent>(OnPlaceTileAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPlaceShieldTileActionEvent>(OnPlaceShieldTileAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPlaceNodeActionEvent>(OnPlaceNodeAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPlaceFactoryActionEvent>(OnPlaceFactoryAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPlaceResourceActionEvent>(OnPlaceResourceAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPlaceStorageActionEvent>(OnPlaceStorageAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPlaceLauncherActionEvent>(OnPlaceLauncherAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobPlaceCoolingActionEvent>(OnPlaceCoolingAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobSpawnBlobbernautActionEvent>(OnSpawnBlobbernautAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobRallyMinionsActionEvent>(OnRallyMinionsAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobSplitConsciousnessActionEvent>(OnSplitConsciousnessAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobShowStatusActionEvent>(OnShowStatusAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobChangeChemicalActionEvent>(OnChangeChemicalAction);
        SubscribeLocalEvent<BlobOvermindComponent, BlobSelectChemicalMessage>(OnSelectChemicalMessage);
        SubscribeLocalEvent<BlobOvermindControllerComponent, BlobSelectChemicalMessage>(OnSelectChemicalMessageOnController);
        SubscribeLocalEvent<BlobOvermindControllerComponent, EntitySpokeEvent>(OnBlobOvermindControllerSpoke);
        SubscribeLocalEvent<BlobOvermindControllerComponent, AfterInteractEvent>(OnControllerAfterInteract);
        SubscribeLocalEvent<BlobOvermindControllerComponent, BeforeRangedInteractEvent>(OnControllerBeforeRangedInteract);
        SubscribeLocalEvent<BlobOvermindControllerComponent, UseInHandEvent>(OnControllerUseInHand);
        SubscribeLocalEvent<BlobStructureComponent, ComponentStartup>(OnBlobStructureStartup);
        SubscribeLocalEvent<BlobStructureComponent, ComponentShutdown>(OnBlobStructureShutdown);
        SubscribeLocalEvent<BlobStructureComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerb);
    }

    private void OnBlobStructureStartup(EntityUid uid, BlobStructureComponent component, ComponentStartup args)
    {
        RemComp<OccluderComponent>(uid);
    }

    private void OnBlobStructureShutdown(EntityUid uid, BlobStructureComponent component, ComponentShutdown args)
    {
        if (Terminating(uid))
            _audio.PlayPvs(BlobGrowSound, uid);

        if (component.OwnerMind is not { } blobId)
            return;

        var prototype = MetaData(uid).EntityPrototype?.ID;
        if (!IsBlobCorePrototype(prototype))
            return;

        var remainingCores = CountOwnedCores(blobId, ignoredUid: uid);
        if (remainingCores > 0)
        {
            PopupToBlobOverminds(blobId, "blob-core-destroyed-secondary");
            return;
        }

        CollapseBlob(blobId);
    }

    private void OnStartup(EntityUid uid, BlobOvermindComponent comp, ComponentStartup args)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);
        _visibility.SetLayer(uid, (ushort) VisibilityFlags.Admin);
        _visibility.RefreshVisibility(uid, visibilityComponent: visibility);
        _eye.RefreshVisibilityMask(uid);

        if (comp.Resources <= 0)
            comp.Resources = comp.StartingResources;

        EnsureComp<AlertsComponent>(uid);

        if (comp.NextEvolutionThreshold <= 0)
            comp.NextEvolutionThreshold = comp.EvolutionThresholdStart;

        InitializeOvermind(uid, comp, refreshActions: true);
    }

    public void InitializeOvermind(EntityUid uid, BlobOvermindComponent comp, bool refreshActions = false)
    {
        if (refreshActions)
            RefreshOvermindActions(uid, comp);

        _blobMob.EnsureBlobRadio(uid);
        EnsureInteractionController(uid, comp);
        UpdateBiomassAlert(uid, comp);
        ApplyChemicalVisual(uid, comp.Chemical);

        if (comp.BlobId is { } blobId)
            ApplyChemicalVisualsForBlob(blobId, comp.Chemical);

        Dirty(uid, comp);
    }

    private void RefreshOvermindActions(EntityUid uid, BlobOvermindComponent comp)
    {
        RemoveActionIfExists(uid, comp.AttackAction);
        RemoveActionIfExists(uid, comp.ConsumeAction);
        RemoveActionIfExists(uid, comp.PlaceTileAction);
        RemoveActionIfExists(uid, comp.PlaceShieldTileAction);
        RemoveActionIfExists(uid, comp.PlaceNodeAction);
        RemoveActionIfExists(uid, comp.PlaceFactoryAction);
        RemoveActionIfExists(uid, comp.PlaceResourceAction);
        RemoveActionIfExists(uid, comp.PlaceStorageAction);
        RemoveActionIfExists(uid, comp.PlaceLauncherAction);
        RemoveActionIfExists(uid, comp.PlaceCoolingAction);
        RemoveActionIfExists(uid, comp.SpawnBlobbernautAction);
        RemoveActionIfExists(uid, comp.RallyMinionsAction);
        RemoveActionIfExists(uid, comp.UpgradeGenerationAction);
        RemoveActionIfExists(uid, comp.UpgradeAttackAction);
        RemoveActionIfExists(uid, comp.UpgradeCapacityAction);
        RemoveActionIfExists(uid, comp.SplitConsciousnessAction);
        RemoveActionIfExists(uid, comp.ShowStatusAction);
        RemoveActionIfExists(uid, comp.ChangeChemicalAction);

        comp.ConsumeAction = _actions.AddAction(uid, comp.ConsumeActionPrototype);
        comp.PlaceNodeAction = _actions.AddAction(uid, comp.PlaceNodeActionPrototype);
        comp.PlaceFactoryAction = _actions.AddAction(uid, comp.PlaceFactoryActionPrototype);
        comp.PlaceResourceAction = _actions.AddAction(uid, comp.PlaceResourceActionPrototype);
        comp.SpawnBlobbernautAction = _actions.AddAction(uid, comp.SpawnBlobbernautActionPrototype);
        comp.RallyMinionsAction = _actions.AddAction(uid, comp.RallyMinionsActionPrototype);
        comp.UpgradeGenerationAction = _actions.AddAction(uid, comp.UpgradeGenerationActionPrototype);
        comp.UpgradeAttackAction = _actions.AddAction(uid, comp.UpgradeAttackActionPrototype);
        comp.UpgradeCapacityAction = _actions.AddAction(uid, comp.UpgradeCapacityActionPrototype);
        comp.SplitConsciousnessAction = _actions.AddAction(uid, comp.SplitConsciousnessActionPrototype);
        comp.ShowStatusAction = _actions.AddAction(uid, comp.ShowStatusActionPrototype);
        comp.ChangeChemicalAction = _actions.AddAction(uid, comp.ChangeChemicalActionPrototype);
    }

    private void OnShutdown(EntityUid uid, BlobOvermindComponent comp, ComponentShutdown args)
    {
        if (Terminating(uid) && comp.BlobId is { } blobId && !HasOtherActiveOvermind(blobId, uid))
            CollapseBlob(blobId, uid);

        if (!Terminating(uid) && TryComp<VisibilityComponent>(uid, out var visibility))
        {
            _visibility.SetLayer(uid, (ushort) VisibilityFlags.Normal);
            _visibility.RefreshVisibility(uid, visibilityComponent: visibility);
        }

        _eye.RefreshVisibilityMask(uid);

        RemoveActionIfExists(uid, comp.AttackAction);
        RemoveActionIfExists(uid, comp.ConsumeAction);
        RemoveActionIfExists(uid, comp.PlaceTileAction);
        RemoveActionIfExists(uid, comp.PlaceShieldTileAction);
        RemoveActionIfExists(uid, comp.PlaceNodeAction);
        RemoveActionIfExists(uid, comp.PlaceFactoryAction);
        RemoveActionIfExists(uid, comp.PlaceResourceAction);
        RemoveActionIfExists(uid, comp.PlaceStorageAction);
        RemoveActionIfExists(uid, comp.PlaceLauncherAction);
        RemoveActionIfExists(uid, comp.PlaceCoolingAction);
        RemoveActionIfExists(uid, comp.SpawnBlobbernautAction);
        RemoveActionIfExists(uid, comp.RallyMinionsAction);
        RemoveActionIfExists(uid, comp.UpgradeGenerationAction);
        RemoveActionIfExists(uid, comp.UpgradeAttackAction);
        RemoveActionIfExists(uid, comp.UpgradeCapacityAction);
        RemoveActionIfExists(uid, comp.SplitConsciousnessAction);
        RemoveActionIfExists(uid, comp.ShowStatusAction);
        RemoveActionIfExists(uid, comp.ChangeChemicalAction);

        if (comp.InteractionController is { } controller && Exists(controller))
            QueueDel(controller);

        comp.InteractionController = null;

        if (TryComp<AlertsComponent>(uid, out var alerts))
            _alerts.ClearAlert((uid, alerts), "BlobBiomass");
    }

    private void OnControllerUseInHand(EntityUid uid, BlobOvermindControllerComponent comp, UseInHandEvent args)
    {
        args.Handled = true;
    }

    private void OnBlobOvermindSpoke(EntityUid uid, BlobOvermindComponent comp, ref EntitySpokeEvent args)
    {
        _blobMob.RelayToBlobRadio(uid, ref args);
    }

    private void OnBlobOvermindControllerSpoke(EntityUid uid, BlobOvermindControllerComponent comp, ref EntitySpokeEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.Message) ||
            comp.Overmind is not { } overmindUid ||
            !TryComp<BlobOvermindComponent>(overmindUid, out var overmind))
        {
            return;
        }

        _blobMob.RelayToBlobRadio(overmindUid, ref args, overmindUid);
    }

    private void OnBlobOvermindGetVis(Entity<BlobOvermindComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Admin;
    }

    private void OnControllerBeforeRangedInteract(EntityUid uid, BlobOvermindControllerComponent controller, BeforeRangedInteractEvent args)
    {
        HandleControllerInteract(uid, controller, args.User, args.Target, args.ClickLocation);
        args.Handled = true;
    }

    private void OnControllerAfterInteract(EntityUid uid, BlobOvermindControllerComponent controller, AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        HandleControllerInteract(uid, controller, args.User, args.Target, args.ClickLocation);
        args.Handled = true;
    }

    private void HandleControllerInteract(EntityUid uid, BlobOvermindControllerComponent controller, EntityUid user, EntityUid? targetEntity, EntityCoordinates clickLocation)
    {
        if (controller.Overmind is not { } overmindUid ||
            !TryComp<BlobOvermindComponent>(overmindUid, out var overmind))
        {
            return;
        }

        var target = GetInteractionTileCoordinates(user, targetEntity, clickLocation);
        if (targetEntity is { } ignoredTarget && IsIgnoredInfrastructure(ignoredTarget))
        {
            TryPlaceOwnedTile(overmindUid, overmind, target);
            return;
        }

        if (targetEntity is { } exactTarget && IsDirectAttackTarget(exactTarget))
            TryPrimaryAttackEntity(overmindUid, overmind, exactTarget);
        else
            TryPlaceOwnedTile(overmindUid, overmind, target);
    }

    private EntityCoordinates GetInteractionTileCoordinates(EntityUid user, EntityUid? target, EntityCoordinates clickLocation)
    {
        if (target is not { } targetUid ||
            !TryComp<PhysicsComponent>(target, out var physics) ||
            !physics.Hard)
        {
            return clickLocation;
        }

        var userMap = _transform.ToMapCoordinates(Transform(user).Coordinates);
        var targetMap = _transform.ToMapCoordinates(Transform(targetUid).Coordinates);

        if (userMap.MapId == MapId.Nullspace || userMap.MapId != targetMap.MapId)
            return clickLocation;

        if (!_mapManager.TryFindGridAt(targetMap, out var gridUid, out var grid))
            return clickLocation;

        var targetTile = _map.CoordinatesToTile(gridUid, grid, targetMap);
        var userTile = _map.CoordinatesToTile(gridUid, grid, userMap);
        var delta = userTile - targetTile;

        if (delta == Vector2i.Zero)
            return clickLocation;

        var frontOffset = Math.Abs(delta.X) >= Math.Abs(delta.Y)
            ? new Vector2i(Math.Sign(delta.X), 0)
            : new Vector2i(0, Math.Sign(delta.Y));

        var frontTile = targetTile + frontOffset;
        return _map.ToCoordinates(gridUid, frontTile, grid);
    }

    private static bool PrototypeLooksLikeInfrastructure(string prototypeId)
    {
        foreach (var token in IgnoredInfrastructurePrototypeTokens)
        {
            if (prototypeId.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void OnGetAlternativeVerb(EntityUid uid, BlobStructureComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!TryComp<BlobOvermindComponent>(args.User, out var overmind))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("blob-action-upgrade-tile-name"),
            Priority = 10,
            Act = () => TryUpgradeOwnedTile(args.User, overmind, Transform(uid).Coordinates)
        });
    }

    private void OnAttackAction(EntityUid uid, BlobOvermindComponent comp, BlobAttackActionEvent args)
    {
        args.Handled = true;

        if (comp.BlobId is not { } blobId)
            return;

        if (comp.Resources < comp.AttackCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-insufficient-resources", ("cost", comp.AttackCost), ("current", comp.Resources)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!TryGetTargetGrid(args.Target, out var gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-invalid-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var snapped = args.Target.SnapToGrid(EntityManager);
        var tile = _map.CoordinatesToTile(gridUid, grid, snapped);
        var sources = CountOwnedAttackSourcesOnTile(gridUid, grid, tile, blobId);
        if (sources <= 0)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-attack-no-source"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (TryPlaceOnIgnoredInfrastructure(uid, comp, args.Target))
            return;

        var hits = AttackEntitiesOnTile(gridUid, grid, tile, comp, sources);
        if (hits <= 0)
        {
            if (TryPlaceOnIgnoredInfrastructure(uid, comp, args.Target))
                return;

            _popup.PopupEntity(Loc.GetString("blob-action-attack-no-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        Spawn("BlobAttackEffect", snapped);

        comp.Resources -= comp.AttackCost;
        SyncNetworkState(blobId, uid, comp);
        _popup.PopupEntity(Loc.GetString("blob-action-attack-hit", ("hits", hits), ("left", comp.Resources)), uid, uid, PopupType.Small);
    }

    private void OnConsumeAction(EntityUid uid, BlobOvermindComponent comp, BlobConsumeTileActionEvent args)
    {
        args.Handled = true;

        if (comp.BlobId is not { } blobId)
            return;

        if (!TryGetTargetGrid(args.Target, out var gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-invalid-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var snapped = args.Target.SnapToGrid(EntityManager);
        var tile = _map.CoordinatesToTile(gridUid, grid, snapped);
        if (!TryGetOwnedBlobStructureOnTile(gridUid, grid, tile, blobId, out var structureUid, out var structureProto) ||
            !TryGetConsumeRefund(structureProto, comp, out var refund))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-consume-invalid"), uid, uid, PopupType.SmallCaution);
            return;
        }

        QueueDel(structureUid);
        comp.Resources = Math.Min(comp.Resources + refund, GetEffectiveMaxResources(blobId, comp));
        SyncNetworkState(blobId, uid, comp);
        _popup.PopupEntity(Loc.GetString("blob-action-consume-success", ("refund", refund), ("current", comp.Resources)), uid, uid, PopupType.Small);
    }

    private void OnUpgradeGenerationAction(EntityUid uid, BlobOvermindComponent comp, BlobUpgradeGenerationActionEvent args)
    {
        args.Handled = true;
        TryApplyUpgrade(uid, comp, ref comp.GenerationUpgradeLevel);
    }

    private void OnUpgradeAttackAction(EntityUid uid, BlobOvermindComponent comp, BlobUpgradeAttackActionEvent args)
    {
        args.Handled = true;
        TryApplyUpgrade(uid, comp, ref comp.AttackUpgradeLevel);
    }

    private void OnUpgradeCapacityAction(EntityUid uid, BlobOvermindComponent comp, BlobUpgradeCapacityActionEvent args)
    {
        args.Handled = true;
        TryApplyUpgrade(uid, comp, ref comp.CapacityUpgradeLevel);
    }

    private void OnPlaceTileAction(EntityUid uid, BlobOvermindComponent comp, BlobPlaceTileActionEvent args)
    {
        args.Handled = true;

        TryPlaceOwnedStructure(uid, comp, args.Target, "BlobTile", comp.TileCost, "blob-action-tile-placed");
    }

    private void OnPlaceShieldTileAction(EntityUid uid, BlobOvermindComponent comp, BlobPlaceShieldTileActionEvent args)
    {
        args.Handled = true;

        TryUpgradeOwnedTile(uid, comp, args.Target);
    }

    private void OnPlaceNodeAction(EntityUid uid, BlobOvermindComponent comp, BlobPlaceNodeActionEvent args)
    {
        args.Handled = true;

        TryPlaceOwnedStructure(uid, comp, args.Target, "BlobNode", comp.NodeCost, "blob-action-node-placed");
    }

    private void OnPlaceFactoryAction(EntityUid uid, BlobOvermindComponent comp, BlobPlaceFactoryActionEvent args)
    {
        args.Handled = true;

        TryPlaceOwnedStructure(uid, comp, args.Target, "BlobFactory", comp.FactoryCost, "blob-action-factory-placed");
    }

    private void OnPlaceResourceAction(EntityUid uid, BlobOvermindComponent comp, BlobPlaceResourceActionEvent args)
    {
        args.Handled = true;

        TryPlaceOwnedStructure(uid, comp, args.Target, "BlobResource", comp.ResourceCost, "blob-action-resource-placed");
    }

    private void OnPlaceStorageAction(EntityUid uid, BlobOvermindComponent comp, BlobPlaceStorageActionEvent args)
    {
        args.Handled = true;

        if (comp.CapacityUpgradeLevel < comp.StorageCapacityLevelRequirement)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-storage-locked", ("required", comp.StorageCapacityLevelRequirement), ("current", comp.CapacityUpgradeLevel)), uid, uid, PopupType.SmallCaution);
            return;
        }

        TryPlaceOwnedStructure(uid, comp, args.Target, "BlobStorage", comp.StorageCost, "blob-action-storage-placed");
    }

    private void OnPlaceLauncherAction(EntityUid uid, BlobOvermindComponent comp, BlobPlaceLauncherActionEvent args)
    {
        args.Handled = true;

        if (comp.AttackUpgradeLevel < comp.LauncherAttackLevelRequirement)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-launcher-locked", ("required", comp.LauncherAttackLevelRequirement), ("current", comp.AttackUpgradeLevel)), uid, uid, PopupType.SmallCaution);
            return;
        }

        TryPlaceOwnedStructure(uid, comp, args.Target, "BlobLauncher", comp.LauncherCost, "blob-action-launcher-placed");
    }

    private void OnPlaceCoolingAction(EntityUid uid, BlobOvermindComponent comp, BlobPlaceCoolingActionEvent args)
    {
        args.Handled = true;

        if (comp.GenerationUpgradeLevel < comp.CoolingGenerationLevelRequirement)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-cooling-locked", ("required", comp.CoolingGenerationLevelRequirement), ("current", comp.GenerationUpgradeLevel)), uid, uid, PopupType.SmallCaution);
            return;
        }

        TryPlaceOwnedStructure(uid, comp, args.Target, "BlobCooling", comp.CoolingCost, "blob-action-cooling-placed");
    }

    private void OnSpawnBlobbernautAction(EntityUid uid, BlobOvermindComponent comp, BlobSpawnBlobbernautActionEvent args)
    {
        args.Handled = true;

        if (comp.AttackUpgradeLevel < comp.BlobbernautAttackLevelRequirement)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-blobbernaut-locked", ("required", comp.BlobbernautAttackLevelRequirement), ("current", comp.AttackUpgradeLevel)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (comp.BlobId is not { } blobId)
            return;

        if (comp.Resources < comp.BlobbernautCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-insufficient-resources", ("cost", comp.BlobbernautCost), ("current", comp.Resources)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!TryGetTargetGrid(args.Target, out var gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-invalid-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var targetCoords = _transform.ToMapCoordinates(args.Target);
        var snapped = args.Target.SnapToGrid(EntityManager);
        var tile = _map.CoordinatesToTile(gridUid, grid, snapped);
        if (!TryGetOwnedBlobStructureOnTile(gridUid, grid, tile, blobId, out var factoryUid, out var factoryProto) ||
            factoryProto != "BlobFactory")
        {
            _popup.PopupEntity(Loc.GetString("blob-action-blobbernaut-no-factory"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var blobbernaut = Spawn("MobBlobbernaut", snapped);
        _audio.PlayPvs(BlobMutateSound, blobbernaut);
        if (TryComp<BlobMobComponent>(blobbernaut, out var blobMob))
        {
            blobMob.OwnerMind = blobId;
            _blobMob.ConfigureMobForOwner(blobbernaut, blobMob);
            Dirty(blobbernaut, blobMob);
        }

        comp.Resources -= comp.BlobbernautCost;
        SyncNetworkState(blobId, uid, comp);

        _popup.PopupEntity(Loc.GetString("blob-action-blobbernaut-spawned", ("left", comp.Resources)), uid, uid, PopupType.Small);
    }

    private void OnRallyMinionsAction(EntityUid uid, BlobOvermindComponent comp, BlobRallyMinionsActionEvent args)
    {
        args.Handled = true;

        if (comp.BlobId is not { } blobId)
            return;

        var snapped = args.Target.SnapToGrid(EntityManager);
        var targetCoords = _transform.ToMapCoordinates(snapped);
        var ordered = 0;
        var query = EntityQueryEnumerator<BlobMobComponent, TransformComponent>();

        while (query.MoveNext(out var mobUid, out var blobMob, out var xform))
        {
            if (blobMob.OwnerMind != blobId)
                continue;

            if (TryComp<MindContainerComponent>(mobUid, out var minionMind) && minionMind.HasMind)
                continue;

            if (_transform.ToMapCoordinates(xform.Coordinates).MapId != targetCoords.MapId)
                continue;

            _steering.Register(mobUid, snapped);

            if (TryComp<HTNComponent>(mobUid, out var htn))
                _npc.WakeNPC(mobUid, htn);

            ordered++;
        }

        if (ordered <= 0)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-rally-no-minions"), uid, uid, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("blob-action-rally-success", ("count", ordered)), uid, uid, PopupType.Small);
    }

    private void OnShowStatusAction(EntityUid uid, BlobOvermindComponent comp, BlobShowStatusActionEvent args)
    {
        args.Handled = true;

        if (comp.BlobId is not { } blobId)
            return;

        var resourceNodes = CountOwnedStructures(blobId, "BlobResource");
        var factories = CountOwnedStructures(blobId, "BlobFactory");
        var cores = CountOwnedCores(blobId);
        var maxResources = GetEffectiveMaxResources(blobId, comp);
        var passiveIncome = GetPassiveIncome(comp);
        var resourceIncome = resourceNodes * comp.ResourceStructureIncome;

        _popup.PopupEntity(
            Loc.GetString(
                "blob-action-status",
                ("current", comp.Resources),
                ("max", maxResources),
                ("passiveIncome", passiveIncome),
                ("passiveInterval", MathF.Round(GetResourceTickInterval(comp), 1)),
                ("resourceIncome", resourceIncome),
                ("resourceInterval", MathF.Round(comp.ResourceStructureTickInterval, 1)),
                ("chemical", Loc.GetString(BlobChemicalVisuals.GetNameLocId(comp.Chemical))),
                ("cores", cores),
                ("resourceNodes", resourceNodes),
                ("factories", factories),
                ("evolution", comp.EvolutionPoints),
                ("next", comp.NextEvolutionThreshold),
                ("generation", comp.GenerationUpgradeLevel),
                ("attack", comp.AttackUpgradeLevel),
                ("capacity", comp.CapacityUpgradeLevel)),
            uid,
            uid,
            PopupType.Medium);
    }

    private void OnChangeChemicalAction(EntityUid uid, BlobOvermindComponent comp, BlobChangeChemicalActionEvent args)
    {
        args.Handled = true;

        var state = GetChemicalMenuState(comp);
        var uiHost = comp.InteractionController is { } controller && Exists(controller)
            ? controller
            : uid;

        _ui.SetUiState(uid, BlobChemicalMenuBuiKey.Key, state);
        if (uiHost != uid)
            _ui.SetUiState(uiHost, BlobChemicalMenuBuiKey.Key, state);

        _ui.TryOpenUi(uiHost, BlobChemicalMenuBuiKey.Key, uid);
    }

    private void OnSelectChemicalMessageOnController(EntityUid uid, BlobOvermindControllerComponent controller, BlobSelectChemicalMessage args)
    {
        if (controller.Overmind is not { } overmindUid || !TryComp<BlobOvermindComponent>(overmindUid, out var comp))
            return;

        HandleSelectChemicalMessage(overmindUid, comp, args);
    }

    private void OnSelectChemicalMessage(EntityUid uid, BlobOvermindComponent comp, BlobSelectChemicalMessage args)
    {
        HandleSelectChemicalMessage(uid, comp, args);
    }

    private void HandleSelectChemicalMessage(EntityUid uid, BlobOvermindComponent comp, BlobSelectChemicalMessage args)
    {
        if (!IsSelectableChemical(args.Chemical))
            return;

        if (args.Chemical == comp.Chemical)
        {
            RefreshChemicalUi(uid, comp);
            return;
        }

        if (comp.Resources < comp.ChemicalChangeCost)
        {
            _popup.PopupEntity(
                Loc.GetString("blob-action-insufficient-resources", ("cost", comp.ChemicalChangeCost), ("current", comp.Resources)),
                uid,
                uid,
                PopupType.SmallCaution);
            RefreshChemicalUi(uid, comp);
            return;
        }

        comp.Resources -= comp.ChemicalChangeCost;
        comp.Chemical = args.Chemical;

        if (comp.BlobId is { } blobId)
        {
            ApplyChemicalVisualsForBlob(blobId, comp.Chemical);
            SyncNetworkState(blobId, uid, comp);
        }
        else
        {
            ApplyChemicalVisual(uid, comp.Chemical);
            UpdateBiomassAlert(uid, comp);
            Dirty(uid, comp);
        }

        RefreshChemicalUi(uid, comp);

        _popup.PopupEntity(
            Loc.GetString(
                "blob-action-change-chemical-success",
                ("chemical", Loc.GetString(BlobChemicalVisuals.GetNameLocId(comp.Chemical))),
                ("left", comp.Resources)),
            uid,
            uid,
            PopupType.Small);
    }

    private void RefreshChemicalUi(EntityUid uid, BlobOvermindComponent comp)
    {
        var state = GetChemicalMenuState(comp);
        _ui.SetUiState(uid, BlobChemicalMenuBuiKey.Key, state);

        if (comp.InteractionController is { } controller && Exists(controller))
            _ui.SetUiState(controller, BlobChemicalMenuBuiKey.Key, state);
    }

    private void OnSplitConsciousnessAction(EntityUid uid, BlobOvermindComponent comp, BlobSplitConsciousnessActionEvent args)
    {
        args.Handled = true;

        if (comp.CapacityUpgradeLevel < comp.SplitCapacityLevelRequirement)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-split-locked", ("required", comp.SplitCapacityLevelRequirement), ("current", comp.CapacityUpgradeLevel)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (comp.BlobId is not { } blobId)
            return;

        if (CountOwnedCores(blobId) >= comp.MaxCoreCount)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-split-limit"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (comp.Resources < comp.SplitConsciousnessCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-insufficient-resources", ("cost", comp.SplitConsciousnessCost), ("current", comp.Resources)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!TryGetTargetGrid(args.Target, out var gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-invalid-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var targetCoords = _transform.ToMapCoordinates(args.Target);
        if (!HasNearbyOwnedBlobStructure(blobId, targetCoords, comp.SpecialStructureRange, "BlobNode"))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-split-needs-node"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var snapped = args.Target.SnapToGrid(EntityManager);
        var tile = _map.CoordinatesToTile(gridUid, grid, snapped);
        if (HasBlobStructureOnTile(gridUid, grid, tile))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-tile-blocked"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var core = Spawn("BlobCoreGhostRole", snapped);
        _audio.PlayPvs(BlobMutateSound, core);
        if (TryComp<BlobStructureComponent>(core, out var blobStructure))
        {
            blobStructure.OwnerMind = blobId;
            Dirty(core, blobStructure);
        }

        ApplyChemicalVisual(core, comp.Chemical);

        comp.Resources -= comp.SplitConsciousnessCost;
        SyncNetworkState(blobId, uid, comp);
        _popup.PopupEntity(Loc.GetString("blob-action-split-created", ("left", comp.Resources)), uid, uid, PopupType.Small);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var processedBlobs = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<BlobOvermindComponent>();
        while (query.MoveNext(out var uid, out var overmind))
        {
            if (overmind.BlobId is not { } blobId)
                continue;

            if (!processedBlobs.Add(blobId))
                continue;

            overmind.ResourceAccumulator += frameTime;
            var resourceNodes = CountOwnedStructures(blobId, "BlobResource");
            if (resourceNodes > 0)
            {
                overmind.ResourceStructureAccumulator += frameTime;
                while (overmind.ResourceStructureAccumulator >= overmind.ResourceStructureTickInterval)
                {
                    overmind.ResourceStructureAccumulator -= overmind.ResourceStructureTickInterval;
                    overmind.Resources = Math.Min(
                        overmind.Resources + resourceNodes * overmind.ResourceStructureIncome,
                        GetEffectiveMaxResources(blobId, overmind));
                }
            }
            else
            {
                overmind.ResourceStructureAccumulator = 0f;
            }

            overmind.AutoSpreadAccumulator += frameTime;
            if (overmind.AutoSpreadAccumulator >= overmind.AutoSpreadInterval)
            {
                overmind.AutoSpreadAccumulator -= overmind.AutoSpreadInterval;
                TryAutomaticSpread(uid, overmind, blobId);
            }

            if (overmind.ResourceAccumulator < GetResourceTickInterval(overmind))
            {
                SyncNetworkState(blobId, uid, overmind);
                continue;
            }

            overmind.ResourceAccumulator -= GetResourceTickInterval(overmind);
            TryGrantEvolutionPoints(uid, overmind, blobId);
            var passiveIncome = GetPassiveIncome(overmind);
            overmind.Resources = Math.Min(overmind.Resources + passiveIncome, GetEffectiveMaxResources(blobId, overmind));
            SyncNetworkState(blobId, uid, overmind);
        }
    }

    private void TryPlaceOwnedStructure(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates target, string prototype, int cost, string successLoc, bool requireNearbyStructure = true)
    {
        if (comp.BlobId is not { } blobId)
            return;

        if (!TryResolveTargetTile(target, out var gridUid, out var grid, out var tile, out var snapped))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-invalid-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var targetCoords = _transform.ToMapCoordinates(snapped);
        var actualCost = GetPlacementCost(gridUid, grid, snapped, prototype, cost, comp);

        if (comp.Resources < actualCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-insufficient-resources", ("cost", actualCost), ("current", comp.Resources)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!ValidatePlacementRules(blobId, targetCoords, prototype, comp, out var failureLoc))
        {
            _popup.PopupEntity(Loc.GetString(failureLoc), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (requireNearbyStructure && prototype == "BlobTile" &&
            !HasNearbyOwnedExpansionSource(
                gridUid,
                grid,
                tile,
                blobId,
                (int) MathF.Round(comp.BuildRange),
                (int) MathF.Round(comp.NodeBuildRange)))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-too-far"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (requireNearbyStructure && prototype != "BlobTile" && !HasNearbyOwnedBlobStructure(blobId, targetCoords, comp.BuildRange))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-too-far"), uid, uid, PopupType.SmallCaution);
            return;
        }
        EntityUid? replacedTile = null;
        if (HasBlobStructureOnTile(gridUid, grid, tile))
        {
            if (prototype != "BlobTile" &&
                TryGetOwnedBlobStructureOnTile(gridUid, grid, tile, blobId, out var ownedStructureUid, out var ownedStructureProto) &&
                IsBlobTilePrototype(ownedStructureProto))
            {
                replacedTile = ownedStructureUid;
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("blob-action-tile-blocked"), uid, uid, PopupType.SmallCaution);
                return;
            }
        }

        if (prototype != "BlobTile" && HasBlockingOccupantOnTile(gridUid, grid, tile, replacedTile))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-tile-blocked"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var tileRef = _map.GetTileRef(gridUid, grid, tile);

        if (prototype != "BlobTile" && TryChewDenseObstacle(tileRef, uid, comp, blobId, actualCost))
            return;

        if (replacedTile is { } replaced && Exists(replaced))
            QueueDel(replaced);

        EnsureBlobTileFoundation(prototype, gridUid, grid, tile, tileRef);

        var structure = Spawn(prototype, snapped);
        _audio.PlayPvs(BlobGrowSound, structure);
        if (TryComp<BlobStructureComponent>(structure, out var blobStructure))
        {
            blobStructure.OwnerMind = blobId;
            Dirty(structure, blobStructure);
        }

        ApplyChemicalVisual(structure, comp.Chemical);

        comp.Resources -= actualCost;
        SyncNetworkState(blobId, uid, comp);

        _popup.PopupEntity(Loc.GetString(successLoc, ("left", comp.Resources)), uid, uid, PopupType.Small);
    }

    private int GetPlacementCost(EntityUid gridUid, MapGridComponent grid, EntityCoordinates target, string prototype, int baseCost, BlobOvermindComponent comp)
    {
        if (prototype != "BlobTile")
            return baseCost;

        return baseCost;
    }

    private bool TryChewDenseObstacle(TileRef tileRef, EntityUid overmindUid, BlobOvermindComponent comp, EntityUid blobId, int cost, bool showPopup = true)
    {
        if (FindDenseObstacle(tileRef) is not { } obstacle)
            return false;

        if (!TryComp<DamageableComponent>(obstacle, out _))
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("blob-action-tile-blocked"), overmindUid, overmindUid, PopupType.SmallCaution);
            return true;
        }

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", comp.DenseObstacleChewDamage);

        if (!_damage.TryChangeDamage(obstacle, damage, true, origin: overmindUid))
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("blob-action-tile-blocked"), overmindUid, overmindUid, PopupType.SmallCaution);
            return true;
        }

        comp.Resources -= cost;
        SyncNetworkState(blobId, overmindUid, comp);
        if (showPopup)
            _popup.PopupEntity(Loc.GetString("blob-action-chew-obstacle", ("left", comp.Resources)), overmindUid, overmindUid, PopupType.Small);
        return true;
    }

    private bool HasDenseObstacle(TileRef tileRef)
    {
        return FindDenseObstacle(tileRef) != null;
    }

    private EntityUid? FindDenseObstacle(TileRef tileRef)
    {
        var entities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInTile(tileRef, entities);

        foreach (var entity in entities)
        {
            if (HasComp<BlobStructureComponent>(entity))
                continue;

            if (IsIgnoredInfrastructure(entity))
                continue;

            if (!TryComp<PhysicsComponent>(entity, out var phys))
                continue;

            if (phys.BodyType != BodyType.Static || !phys.Hard)
                continue;

            return entity;
        }

        return null;
    }

    private void TryUpgradeOwnedTile(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates target)
    {
        if (!TryBeginTileAction(comp))
            return;

        if (comp.BlobId is not { } blobId)
            return;

        if (!TryGetTargetGrid(target, out var gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-invalid-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var snapped = target.SnapToGrid(EntityManager);
        var tile = _map.CoordinatesToTile(gridUid, grid, snapped);
        if (!HasNearbyOwnedExpansionSource(
            gridUid,
            grid,
            tile,
            blobId,
            (int) MathF.Round(comp.BuildRange),
            (int) MathF.Round(comp.NodeBuildRange)))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-too-far"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!TryGetOwnedBlobStructureOnTile(gridUid, grid, tile, blobId, out var structureUid, out var structureProto))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-upgrade-invalid"), uid, uid, PopupType.SmallCaution);
            return;
        }

        string newPrototype;
        int cost;
        string successLoc;

        switch (structureProto)
        {
            case "BlobTile":
                newPrototype = "BlobTileShield";
                cost = comp.ShieldTileCost;
                successLoc = "blob-action-shield-placed";
                break;
            case "BlobTileShield":
                newPrototype = "BlobTileReflective";
                cost = comp.ReflectiveTileCost;
                successLoc = "blob-action-reflective-placed";
                break;
            default:
                _popup.PopupEntity(Loc.GetString("blob-action-upgrade-invalid"), uid, uid, PopupType.SmallCaution);
                return;
        }

        if (comp.Resources < cost)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-insufficient-resources", ("cost", cost), ("current", comp.Resources)), uid, uid, PopupType.SmallCaution);
            return;
        }

        QueueDel(structureUid);
        var structure = Spawn(newPrototype, snapped);
        _audio.PlayPvs(BlobMutateSound, structure);
        if (TryComp<BlobStructureComponent>(structure, out var blobStructure))
        {
            blobStructure.OwnerMind = blobId;
            Dirty(structure, blobStructure);
        }

        ApplyChemicalVisual(structure, comp.Chemical);

        comp.Resources -= cost;
        SyncNetworkState(blobId, uid, comp);
        _popup.PopupEntity(Loc.GetString(successLoc, ("left", comp.Resources)), uid, uid, PopupType.Small);
    }

    private void OnGhostRoleSpawnerUsed(EntityUid uid, BlobOvermindComponent comp, GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<BlobStructureComponent>(args.Spawner, out var coreStructure) || coreStructure.OwnerMind is not { } blobId)
            return;

        comp.BlobId = blobId;
        comp.Chemical = GetChemicalForBlob(blobId);
        SyncStateFromExistingOvermind(blobId, uid, comp);
        InitializeOvermind(uid, comp, refreshActions: true);
    }

    private void RemoveActionIfExists(EntityUid uid, EntityUid? action)
    {
        if (action is { } actionUid && Exists(actionUid))
            _actions.RemoveAction(uid, actionUid);
    }

    private void EnsureInteractionController(EntityUid uid, BlobOvermindComponent comp)
    {
        var hands = EnsureComp<HandsComponent>(uid);

        const string handId = "blob-control";
        _hands.AddHand((uid, hands), handId, HandLocation.Middle);
        _hands.SetActiveHand((uid, hands), handId);

        if (comp.InteractionController is { } existing && Exists(existing))
        {
            EnsureComp<BlobOvermindControllerComponent>(existing).Overmind = uid;
            EnsureControllerRadioProfile(existing);

            if (!_hands.IsHolding((uid, hands), existing, out _))
                _hands.TryForcePickup((uid, hands), existing, handId, checkActionBlocker: false, animate: false);

            return;
        }

        var controller = Spawn("BlobOvermindController", Transform(uid).Coordinates);
        EnsureComp<BlobOvermindControllerComponent>(controller).Overmind = uid;
        EnsureControllerRadioProfile(controller);
        comp.InteractionController = controller;
        _hands.TryForcePickup((uid, hands), controller, handId, checkActionBlocker: false, animate: false);
    }

    private void EnsureControllerRadioProfile(EntityUid controller)
    {
        _blobMob.EnsureBlobRadio(controller);
        _npcFaction.AddFaction(controller, BlobFactionId);
    }

    private void TryPrimaryAttack(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates target)
    {
        if (!TryBeginAttack(comp))
            return;

        if (comp.BlobId is not { } blobId)
            return;

        if (comp.Resources < comp.AttackCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-insufficient-resources", ("cost", comp.AttackCost), ("current", comp.Resources)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!TryGetTargetGrid(target, out var gridUid, out var grid))
            return;

        var snapped = target.SnapToGrid(EntityManager);
        var tile = _map.CoordinatesToTile(gridUid, grid, snapped);
        var sources = CountOwnedAttackSourcesOnTile(gridUid, grid, tile, blobId);
        if (sources <= 0)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-attack-no-source"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (TryPlaceOnIgnoredInfrastructure(uid, comp, target))
            return;

        var hits = AttackEntitiesOnTile(gridUid, grid, tile, comp, sources);
        if (hits <= 0)
        {
            if (TryPlaceOnIgnoredInfrastructure(uid, comp, target))
                return;

            _popup.PopupEntity(Loc.GetString("blob-action-attack-no-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        Spawn("BlobAttackEffect", snapped);

        comp.Resources -= comp.AttackCost;
        SyncNetworkState(blobId, uid, comp);
        _popup.PopupEntity(Loc.GetString("blob-action-attack-hit", ("hits", hits), ("left", comp.Resources)), uid, uid, PopupType.Small);
    }

    private void TryPrimaryAttackEntity(EntityUid uid, BlobOvermindComponent comp, EntityUid targetEntity)
    {
        if (!TryBeginAttack(comp))
            return;

        if (comp.BlobId is not { } blobId)
            return;

        if (comp.Resources < comp.AttackCost)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-insufficient-resources", ("cost", comp.AttackCost), ("current", comp.Resources)), uid, uid, PopupType.SmallCaution);
            return;
        }

        var target = Transform(targetEntity).Coordinates;
        if (!TryGetTargetGrid(target, out var gridUid, out var grid))
            return;

        var snapped = target.SnapToGrid(EntityManager);
        var tile = _map.CoordinatesToTile(gridUid, grid, snapped);
        var sources = CountOwnedAttackSourcesOnTile(gridUid, grid, tile, blobId);
        if (sources <= 0 && TryGetOwnedBlobStructureOnTile(gridUid, grid, tile, blobId, out _, out _))
            sources = 1;

        if (sources <= 0)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-attack-no-source"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!TryApplyBlobAttackToEntity(targetEntity, comp, sources))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-attack-no-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        Spawn("BlobAttackEffect", snapped);

        comp.Resources -= comp.AttackCost;
        SyncNetworkState(blobId, uid, comp);
        _popup.PopupEntity(Loc.GetString("blob-action-attack-hit", ("hits", 1), ("left", comp.Resources)), uid, uid, PopupType.Small);
    }

    private void TryPlaceOwnedTile(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates target)
    {
        if (!TryBeginTileAction(comp))
            return;

        if (comp.BlobId is not { } blobId)
            return;

        if (!TryResolveTargetTile(target, out var gridUid, out var grid, out var tile, out _))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-invalid-target"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!HasNearbyOwnedExpansionSource(
            gridUid,
            grid,
            tile,
            blobId,
            (int) MathF.Round(comp.BuildRange),
            (int) MathF.Round(comp.NodeBuildRange)))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-too-far"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!HasOwnedCardinalAdjacentBlobStructure(gridUid, grid, tile, blobId))
        {
            _popup.PopupEntity(Loc.GetString("blob-action-too-far"), uid, uid, PopupType.SmallCaution);
            return;
        }

        TryPlaceOwnedStructure(uid, comp, target, "BlobTile", comp.TileCost, "blob-action-tile-placed", requireNearbyStructure: false);
    }

    private bool IsDirectAttackTarget(EntityUid entity)
    {
        if (HasComp<BlobStructureComponent>(entity) || HasComp<BlobMobComponent>(entity) || HasComp<BlobOvermindComponent>(entity))
            return false;

        if (IsIgnoredInfrastructure(entity))
            return false;

        if (!HasComp<DamageableComponent>(entity))
            return false;

        if (!TryComp<MobStateComponent>(entity, out var mobState))
            return true;

        return mobState.CurrentState != MobState.Dead;
    }

    private bool IsIgnoredInfrastructure(EntityUid entity)
    {
        // Subfloor entities (cables, pipes, etc.) and catwalks should not be attacked
        // and should not block blob placement/spread.
        if (HasComp<SubFloorHideComponent>(entity) ||
            HasComp<CableComponent>(entity) ||
            HasComp<DisposalTubeComponent>(entity) ||
            HasComp<PipeRestrictOverlapComponent>(entity) ||
            HasComp<PipeAppearanceComponent>(entity) ||
            HasComp<AtmosPipeLayersComponent>(entity) ||
            _tags.HasTag(entity, CatwalkTag))
        {
            return true;
        }

        if (!TryComp(entity, out MetaDataComponent? metaData))
            return false;

        var prototypeId = metaData.EntityPrototype?.ID;
        if (string.IsNullOrEmpty(prototypeId))
            return false;

        return PrototypeLooksLikeInfrastructure(prototypeId);
    }

    private bool TryPlaceOnIgnoredInfrastructure(EntityUid uid, BlobOvermindComponent comp, EntityCoordinates target)
    {
        if (!HasIgnoredInfrastructureOnTile(target))
            return false;

        TryPlaceOwnedTile(uid, comp, target);
        return true;
    }

    private bool HasIgnoredInfrastructureOnTile(EntityCoordinates target)
    {
        if (!TryGetTargetGrid(target, out var gridUid, out var grid))
            return false;

        var snapped = target.SnapToGrid(EntityManager);
        var tile = _map.CoordinatesToTile(gridUid, grid, snapped);

        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var entity, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (_map.CoordinatesToTile(gridUid, grid, xform.Coordinates) != tile)
                continue;

            if (HasComp<BlobStructureComponent>(entity) || HasComp<BlobMobComponent>(entity) || HasComp<BlobOvermindComponent>(entity))
                continue;

            if (IsIgnoredInfrastructure(entity))
                return true;
        }

        return false;
    }

    private BlobChemicalType GetChemicalForBlob(EntityUid blobId)
    {
        var query = EntityQueryEnumerator<BlobOvermindComponent>();
        while (query.MoveNext(out _, out var overmind))
        {
            if (overmind.BlobId == blobId)
                return overmind.Chemical;
        }

        return BlobChemicalType.Toxin;
    }

    private bool TryGetTargetGrid(EntityCoordinates target, out EntityUid gridUid, out MapGridComponent grid)
    {
        gridUid = EntityUid.Invalid;
        grid = default!;

        if (target.GetGridUid(EntityManager) is { } directUid && TryComp<MapGridComponent>(directUid, out var directGrid))
        {
            gridUid = directUid;
            grid = directGrid;
            return true;
        }

        var mapCoords = _transform.ToMapCoordinates(target);
        if (mapCoords.MapId == MapId.Nullspace)
            return false;

        if (_mapManager.TryFindGridAt(mapCoords, out var foundUid, out var foundGrid))
        {
            gridUid = foundUid;
            grid = foundGrid;
            return true;
        }

        if (!_map.TryGetMap(mapCoords.MapId, out var mapUid))
            return false;

        var nearbyGrids = new List<Entity<MapGridComponent>>();
        var range = new Vector2(0.75f, 0.75f);
        var bounds = new Box2(mapCoords.Position - range, mapCoords.Position + range);
        _mapManager.FindGridsIntersecting(mapUid.Value, bounds, ref nearbyGrids, approx: true, includeMap: false);

        var bestDistance = float.MaxValue;
        foreach (var candidate in nearbyGrids)
        {
            var candidateTile = _map.CoordinatesToTile(candidate.Owner, candidate.Comp, mapCoords);
            var candidateCenter = _transform.ToMapCoordinates(_map.ToCenterCoordinates(candidate.Owner, candidateTile, candidate.Comp));
            var distance = (candidateCenter.Position - mapCoords.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            gridUid = candidate.Owner;
            grid = candidate.Comp;
        }

        return gridUid != EntityUid.Invalid;
    }

    private bool TryResolveTargetTile(EntityCoordinates target, out EntityUid gridUid, out MapGridComponent grid, out Vector2i tile, out EntityCoordinates snapped)
    {
        tile = default;
        snapped = EntityCoordinates.Invalid;

        if (!TryGetTargetGrid(target, out gridUid, out grid))
            return false;

        var mapCoords = _transform.ToMapCoordinates(target);
        tile = _map.CoordinatesToTile(gridUid, grid, mapCoords);
        snapped = _map.ToCoordinates(gridUid, tile, grid);
        return true;
    }

    private int CountOwnedAttackSourcesOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i targetTile, EntityUid ownerMind)
    {
        var count = 0;
        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent>();
        while (query.MoveNext(out _, out var blobStructure, out var xform))
        {
            if (blobStructure.OwnerMind != ownerMind)
                continue;

            if (xform.GridUid != gridUid)
                continue;

            var structureTile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            var delta = structureTile - targetTile;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) != 1)
                continue;

            count++;
        }

        return count;
    }

    private bool HasOwnedCardinalAdjacentBlobStructure(EntityUid gridUid, MapGridComponent grid, Vector2i targetTile, EntityUid ownerMind)
    {
        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent>();
        while (query.MoveNext(out _, out var blobStructure, out var xform))
        {
            if (blobStructure.OwnerMind != ownerMind)
                continue;

            if (xform.GridUid != gridUid)
                continue;

            var structureTile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            var delta = structureTile - targetTile;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) != 1)
                continue;

            return true;
        }

        return false;
    }

    private int AttackEntitiesOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i targetTile, BlobOvermindComponent comp, int sources)
    {
        var hits = 0;
        var targets = new List<EntityUid>();
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var targetUid, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            if (HasComp<BlobStructureComponent>(targetUid) || HasComp<BlobMobComponent>(targetUid) || HasComp<BlobOvermindComponent>(targetUid))
                continue;

            if (IsIgnoredInfrastructure(targetUid))
                continue;

            if (_map.CoordinatesToTile(gridUid, grid, xform.Coordinates) != targetTile)
                continue;

            if (!TryComp<DamageableComponent>(targetUid, out _))
                continue;

            targets.Add(targetUid);
        }

        foreach (var targetUid in targets)
        {
            if (TryApplyBlobAttackToEntity(targetUid, comp, sources))
                hits++;
        }

        return hits;
    }

    private bool TryApplyBlobAttackToEntity(EntityUid targetUid, BlobOvermindComponent comp, int sources)
    {
        if (Deleted(targetUid) || !TryComp<DamageableComponent>(targetUid, out _))
            return false;

        var chemicalDamage = GetAttackChemicalDamage(comp);
        var damage = new DamageSpecifier();
        if (!HasComp<MobStateComponent>(targetUid))
        {
            damage.DamageDict.Add("Blunt", comp.StructureAttackDamage);
        }
        else
        {
            damage.DamageDict.Add("Blunt", GetActiveAttackDamage(comp, sources));

            switch (comp.Chemical)
            {
                case BlobChemicalType.Toxin:
                    damage.DamageDict.Add("Poison", chemicalDamage);
                    break;
                case BlobChemicalType.Incendiary:
                    damage.DamageDict.Add("Heat", chemicalDamage);
                    break;
                case BlobChemicalType.Electromagnetic:
                    damage.DamageDict.Add("Heat", chemicalDamage + 2);
                    break;
                case BlobChemicalType.DistributedNeurons:
                    damage.DamageDict.Add("Poison", chemicalDamage + 1);
                    break;
                case BlobChemicalType.KineticGelatin:
                    damage.DamageDict.Add("Stamina", chemicalDamage + 6);
                    break;
                case BlobChemicalType.RadioactiveGel:
                    damage.DamageDict.Add("Poison", Math.Max(1, chemicalDamage - 1));
                    damage.DamageDict.Add("Radiation", Math.Max(1, chemicalDamage - 1));
                    break;
                case BlobChemicalType.LexorinJelly:
                    damage.DamageDict.Add("Asphyxiation", chemicalDamage + 4);
                    break;
                case BlobChemicalType.CryogenicLiquid:
                    damage.DamageDict.Add("Cold", Math.Max(1, chemicalDamage));
                    damage.DamageDict.Add("Stamina", Math.Max(1, chemicalDamage + 2));
                    break;
                case BlobChemicalType.Sorium:
                    damage.DamageDict.Add("Stamina", chemicalDamage + 4);
                    break;
                case BlobChemicalType.EnvenomedFilaments:
                    damage.DamageDict.Add("Poison", chemicalDamage + 2);
                    damage.DamageDict.Add("Stamina", Math.Max(1, chemicalDamage));
                    break;
                case BlobChemicalType.ParalyticToxins:
                    damage.DamageDict.Add("Poison", chemicalDamage);
                    damage.DamageDict.Add("Stamina", Math.Max(1, chemicalDamage - 1));
                    break;
                case BlobChemicalType.Regenerative:
                    damage.DamageDict.Add("Poison", Math.Max(1, chemicalDamage - 2));
                    break;
            }
        }

        if (!_damage.TryChangeDamage(targetUid, damage, true))
            return false;

        _audio.PlayPvs(BlobAttackSound, targetUid);
        ApplyAttackSecondaryEffects(targetUid, comp);
        return true;
    }

    private void EnsureAttackInfection(EntityUid targetUid, BlobOvermindComponent comp, float transformDelay)
    {
        if (comp.BlobId is not { } blobId)
            return;

        _infection.EnsurePendingInfection(targetUid, blobId, comp.Chemical, transformDelay);
    }

    private void ApplyAttackSecondaryEffects(EntityUid targetUid, BlobOvermindComponent comp)
    {
        if (!TryComp<MobStateComponent>(targetUid, out var mobState) || mobState.CurrentState == MobState.Dead)
            return;

        switch (comp.Chemical)
        {
            case BlobChemicalType.Toxin:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, comp.AttackChemicalDuration);
                EnsureAttackInfection(targetUid, comp, 8f);
                break;
            case BlobChemicalType.Incendiary:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, comp.AttackChemicalDuration);
                break;
            case BlobChemicalType.Electromagnetic:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, comp.AttackChemicalDuration);
                break;
            case BlobChemicalType.DistributedNeurons:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, Math.Max(2f, comp.AttackChemicalDuration - 1f));
                EnsureAttackInfection(targetUid, comp, 3f);
                break;
            case BlobChemicalType.RadioactiveGel:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, comp.AttackChemicalDuration + 1f);
                break;
            case BlobChemicalType.LexorinJelly:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, comp.AttackChemicalDuration + 1f);
                break;
            case BlobChemicalType.CryogenicLiquid:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, comp.AttackChemicalDuration);
                break;
            case BlobChemicalType.Sorium:
                ApplySoriumKnockbackFromTile(targetUid, comp.SoriumThrowDistance);
                break;
            case BlobChemicalType.EnvenomedFilaments:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, comp.AttackChemicalDuration + 1f);
                _stun.TryAddStunDuration(targetUid, TimeSpan.FromSeconds(1.5));
                break;
            case BlobChemicalType.ParalyticToxins:
                _chemistry.ApplyChemicalEffect(targetUid, comp.Chemical, comp.AttackChemicalDuration);
                _stun.TryKnockdown(targetUid, TimeSpan.FromSeconds(1.25), true);
                break;
        }
    }

    private void ApplySoriumKnockbackFromTile(EntityUid targetUid, float distance)
    {
        var targetCoords = _transform.ToMapCoordinates(Transform(targetUid).Coordinates);
        var sourceCoords = targetCoords.Position + new Vector2(-1f, 0f);
        var pushDir = targetCoords.Position - sourceCoords;
        if (pushDir.LengthSquared() < 0.01f)
            pushDir = new Vector2(1f, 0f);
        else
            pushDir = pushDir.Normalized() * distance;

        _throwing.TryThrow(targetUid, pushDir, 8f);
    }

    private bool HasNearbyOwnedBlobStructure(EntityUid mindId, MapCoordinates target, float range, string? prototype = null)
    {
        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var blobStructure, out var xform, out var metaData))
        {
            if (blobStructure.OwnerMind != mindId)
                continue;

            if (prototype != null)
            {
                var prototypeId = metaData.EntityPrototype?.ID;
                if (prototypeId == null || prototypeId != prototype)
                    continue;
            }

            var structureCoords = _transform.ToMapCoordinates(xform.Coordinates);
            if (structureCoords.MapId != target.MapId)
                continue;

            if ((structureCoords.Position - target.Position).Length() <= range)
                return true;
        }

        return false;
    }

    private bool TryFindNearbyOwnedBlobStructure(EntityUid mindId, MapCoordinates target, float range, string prototype, out EntityUid? structureUid)
    {
        structureUid = null;
        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var blobStructure, out var xform, out var metaData))
        {
            if (blobStructure.OwnerMind != mindId)
                continue;

            var prototypeId = metaData.EntityPrototype?.ID;
            if (prototypeId == null || prototypeId != prototype)
                continue;

            var structureCoords = _transform.ToMapCoordinates(xform.Coordinates);
            if (structureCoords.MapId != target.MapId)
                continue;

            if ((structureCoords.Position - target.Position).Length() > range)
                continue;

            structureUid = uid;
            return true;
        }

        return false;
    }

    private bool HasBlobStructureOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            var structureTile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            if (structureTile == tile)
                return true;
        }

        return false;
    }

    private bool HasBlockingOccupantOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile, EntityUid? ignored = null)
    {
        var entities = new HashSet<EntityUid>();
        var tileRef = _map.GetTileRef(gridUid, grid, tile);
        _lookup.GetEntitiesInTile(tileRef, entities);

        foreach (var entity in entities)
        {
            if (entity == ignored)
                continue;

            if (HasComp<BlobStructureComponent>(entity) || HasComp<BlobOvermindComponent>(entity) || HasComp<BlobOvermindControllerComponent>(entity))
                continue;

            if (IsIgnoredInfrastructure(entity))
                continue;

            return true;
        }

        return false;
    }

    private bool TryGetOwnedBlobStructureOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile, EntityUid ownerMind, out EntityUid structureUid, out string? prototype)
    {
        structureUid = EntityUid.Invalid;
        prototype = null;

        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var blobStructure, out var xform, out var metaData))
        {
            if (blobStructure.OwnerMind != ownerMind)
                continue;

            if (xform.GridUid != gridUid)
                continue;

            var structureTile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            if (structureTile != tile)
                continue;

            structureUid = uid;
            prototype = metaData.EntityPrototype?.ID;
            return true;
        }

        return false;
    }

    private bool ValidatePlacementRules(EntityUid mindId, MapCoordinates targetCoords, string prototype, BlobOvermindComponent comp, out string failureLoc)
    {
        failureLoc = string.Empty;

        if (prototype == "BlobNode" && HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.NodeMinDistance, "BlobNode"))
        {
            failureLoc = "blob-action-node-too-close";
            return false;
        }

        if (prototype == "BlobFactory")
        {
            if (!HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.SpecialStructureRange, "BlobNode") &&
                !HasNearbyOwnedBlobCore(mindId, targetCoords, comp.SpecialStructureRange))
            {
                failureLoc = "blob-action-factory-needs-node";
                return false;
            }

            if (HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.FactoryMinDistance, "BlobFactory"))
            {
                failureLoc = "blob-action-factory-too-close";
                return false;
            }
        }

        if (prototype == "BlobResource")
        {
            if (!HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.SpecialStructureRange, "BlobNode") &&
                !HasNearbyOwnedBlobCore(mindId, targetCoords, comp.SpecialStructureRange))
            {
                failureLoc = "blob-action-resource-needs-node";
                return false;
            }

            if (HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.ResourceMinDistance, "BlobResource"))
            {
                failureLoc = "blob-action-resource-too-close";
                return false;
            }
        }

        if (prototype == "BlobStorage")
        {
            if (!HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.SpecialStructureRange, "BlobNode") &&
                !HasNearbyOwnedBlobCore(mindId, targetCoords, comp.SpecialStructureRange))
            {
                failureLoc = "blob-action-storage-needs-node";
                return false;
            }

            if (HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.StorageMinDistance, "BlobStorage"))
            {
                failureLoc = "blob-action-storage-too-close";
                return false;
            }
        }

        if (prototype == "BlobLauncher")
        {
            if (!HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.SpecialStructureRange, "BlobNode") &&
                !HasNearbyOwnedBlobCore(mindId, targetCoords, comp.SpecialStructureRange))
            {
                failureLoc = "blob-action-launcher-needs-node";
                return false;
            }

            if (HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.LauncherMinDistance, "BlobLauncher"))
            {
                failureLoc = "blob-action-launcher-too-close";
                return false;
            }
        }

        if (prototype == "BlobCooling")
        {
            if (!HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.SpecialStructureRange, "BlobNode") &&
                !HasNearbyOwnedBlobCore(mindId, targetCoords, comp.SpecialStructureRange))
            {
                failureLoc = "blob-action-cooling-needs-node";
                return false;
            }

            if (HasNearbyOwnedBlobStructure(mindId, targetCoords, comp.CoolingMinDistance, "BlobCooling"))
            {
                failureLoc = "blob-action-cooling-too-close";
                return false;
            }
        }

        return true;
    }

    private bool HasNearbyOwnedBlobCore(EntityUid blobId, MapCoordinates target, float range)
    {
        return HasNearbyOwnedBlobStructure(blobId, target, range, "BlobCore") ||
               HasNearbyOwnedBlobStructure(blobId, target, range, "BlobCoreGhostRole");
    }

    private bool HasNearbyOwnedExpansionSource(EntityUid gridUid, MapGridComponent grid, Vector2i targetTile, EntityUid blobId, int coreRange, int nodeRange)
    {
        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var blobStructure, out var xform, out var metaData))
        {
            if (blobStructure.OwnerMind != blobId || xform.GridUid != gridUid)
                continue;

            var prototype = metaData.EntityPrototype?.ID;
            var range = prototype == "BlobNode"
                ? nodeRange
                : IsBlobCorePrototype(prototype)
                    ? coreRange
                    : -1;

            if (range < 0)
                continue;

            var structureTile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            if (!IsWithinTileRange(structureTile, targetTile, range))
                continue;

            return true;
        }

        return false;
    }

    private static bool IsWithinTileRange(Vector2i sourceTile, Vector2i targetTile, int range)
    {
        var delta = sourceTile - targetTile;
        return Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y)) <= range;
    }

    private void TryAutomaticSpread(EntityUid uid, BlobOvermindComponent comp, EntityUid blobId)
    {
        var sources = new List<(EntityUid GridUid, Vector2i SourceTile, int Range)>();
        var query = EntityQueryEnumerator<BlobStructureComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var structure, out var xform, out var meta))
        {
            if (structure.OwnerMind != blobId)
                continue;

            if (!TryGetAutomaticSpreadRange(meta.EntityPrototype?.ID, comp, out var range))
                continue;

            if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            sources.Add((gridUid, _map.CoordinatesToTile(gridUid, grid, xform.Coordinates), range));
        }

        foreach (var (gridUid, sourceTile, range) in sources)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            TryAutomaticSpreadFromSource(uid, comp, blobId, gridUid, grid, sourceTile, range);
        }
    }

    private bool TryAutomaticSpreadFromSource(EntityUid uid, BlobOvermindComponent comp, EntityUid blobId, EntityUid gridUid, MapGridComponent grid, Vector2i sourceTile, int range)
    {
        for (var distance = 1; distance <= range; distance++)
        {
            for (var x = -distance; x <= distance; x++)
            {
                for (var y = -distance; y <= distance; y++)
                {
                    var targetTile = sourceTile + new Vector2i(x, y);
                    if (!IsWithinTileRange(sourceTile, targetTile, range))
                        continue;

                    if (!_map.TryGetTileRef(gridUid, grid, targetTile, out var tileRef))
                        continue;

                    if (HasBlobStructureOnTile(gridUid, grid, targetTile))
                        continue;

                    if (!HasOwnedCardinalAdjacentBlobStructure(gridUid, grid, targetTile, blobId))
                        continue;

                    if (HasDenseObstacle(tileRef))
                    {
                        if (TryChewDenseObstacle(tileRef, uid, comp, blobId, 0, showPopup: false))
                            return true;

                        continue;
                    }

                    if (HasBlockingOccupantOnTile(gridUid, grid, targetTile))
                        continue;

                    var targetCoords = _map.GridTileToLocal(gridUid, grid, targetTile);

                    EnsureBlobTileFoundation("BlobTile", gridUid, grid, targetTile, tileRef);

                    var structure = Spawn("BlobTile", targetCoords);
                    _audio.PlayPvs(BlobGrowSound, structure);
                    if (TryComp<BlobStructureComponent>(structure, out var blobStructure))
                    {
                        blobStructure.OwnerMind = blobId;
                        Dirty(structure, blobStructure);
                    }

                    ApplyChemicalVisual(structure, comp.Chemical);
                    return true;
                }
            }
        }

        return false;
    }
    private bool IsSpaceTile(TileRef tileRef)
    {
        var tileDef = (ContentTileDefinition) _tileDefs[tileRef.Tile.TypeId];
        return tileDef.ID == ContentTileDefinition.SpaceID || tileDef.BaseTurf == ContentTileDefinition.SpaceID;
    }

    private void EnsureBlobCoverageTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile, TileRef tileRef)
    {
        if (!IsSpaceTile(tileRef))
            return;

        _map.SetTile(gridUid, grid, tile, new Tile(_tileDefs["Plating"].TileId));
    }

    private void EnsureBlobTileFoundation(string prototype, EntityUid gridUid, MapGridComponent grid, Vector2i tile, TileRef tileRef)
    {
        if (prototype != "BlobTile")
            return;

        EnsureBlobCoverageTile(gridUid, grid, tile, tileRef);
    }

    private static bool TryGetAutomaticSpreadRange(string? prototype, BlobOvermindComponent comp, out int range)
    {
        if (prototype == "BlobNode")
        {
            range = (int) MathF.Round(comp.NodeBuildRange);
            return true;
        }

        if (prototype == "BlobCore" || prototype == "BlobCoreGhostRole")
        {
            range = (int) MathF.Round(comp.BuildRange);
            return true;
        }

        range = 0;
        return false;
    }

    private int CountOwnedCores(EntityUid blobId, EntityUid? ignoredUid = null)
    {
        var count = 0;
        var query = EntityQueryEnumerator<BlobStructureComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var blobStructure, out var metaData))
        {
            if (uid == ignoredUid)
                continue;

            if (blobStructure.OwnerMind != blobId)
                continue;

            if (!IsBlobCorePrototype(metaData.EntityPrototype?.ID))
                continue;

            count++;
        }

        return count;
    }

    public void NeutralizeBlob(EntityUid blobId, EntityUid? ignoredOvermind = null)
    {
        CollapseBlob(blobId, ignoredOvermind);
    }

    private void CollapseBlob(EntityUid blobId, EntityUid? ignoredOvermind = null)
    {
        var structures = EntityQueryEnumerator<BlobStructureComponent>();
        while (structures.MoveNext(out var uid, out var structure))
        {
            if (structure.OwnerMind != blobId)
                continue;

            ApplyNeutralBlobVisual(uid);
            structure.OwnerMind = null;
            Dirty(uid, structure);
        }

        var mobs = EntityQueryEnumerator<BlobMobComponent>();
        while (mobs.MoveNext(out var uid, out var mob))
        {
            if (mob.OwnerMind != blobId)
                continue;

            QueueDel(uid);
        }

        var overminds = EntityQueryEnumerator<BlobOvermindComponent>();
        while (overminds.MoveNext(out var uid, out var overmind))
        {
            if (uid == ignoredOvermind)
                continue;

            if (overmind.BlobId != blobId)
                continue;

            _popup.PopupEntity(Loc.GetString("blob-core-destroyed"), uid, uid, PopupType.MediumCaution);
            QueueDel(uid);
        }
    }

    private bool HasOtherActiveOvermind(EntityUid blobId, EntityUid ignoredOvermind)
    {
        var overminds = EntityQueryEnumerator<BlobOvermindComponent>();
        while (overminds.MoveNext(out var uid, out var overmind))
        {
            if (uid == ignoredOvermind)
                continue;

            if (overmind.BlobId != blobId || Terminating(uid))
                continue;

            return true;
        }

        return false;
    }

    private void PopupToBlobOverminds(EntityUid blobId, string locId)
    {
        var overminds = EntityQueryEnumerator<BlobOvermindComponent>();
        while (overminds.MoveNext(out var uid, out var overmind))
        {
            if (overmind.BlobId != blobId)
                continue;

            _popup.PopupEntity(Loc.GetString(locId), uid, uid, PopupType.MediumCaution);
        }
    }

    private void ApplyNeutralBlobVisual(EntityUid uid)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        _appearance.SetData(uid, Content.Shared.Imperial.Blob.BlobVisuals.Color, Color.Gray, appearance);
    }

    private static bool IsBlobCorePrototype(string? prototype)
    {
        return prototype == "BlobCore" || prototype == "BlobCoreGhostRole";
    }

    private static bool IsBlobTilePrototype(string? prototype)
    {
        return prototype == "BlobTile" || prototype == "BlobTileShield" || prototype == "BlobTileReflective";
    }

    private int CountOwnedStructures(EntityUid mindId, string prototype)
    {
        var count = 0;
        var query = EntityQueryEnumerator<BlobStructureComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var blobStructure, out var metaData))
        {
            if (blobStructure.OwnerMind != mindId)
                continue;

            if (metaData.EntityPrototype?.ID != prototype)
                continue;

            count++;
        }

        return count;
    }

    private int CountOwnedBiomass(EntityUid blobId)
    {
        var count = 0;
        var query = EntityQueryEnumerator<BlobStructureComponent>();
        while (query.MoveNext(out _, out var blobStructure))
        {
            if (blobStructure.OwnerMind != blobId)
                continue;

            count++;
        }

        return count;
    }

    private void TryGrantEvolutionPoints(EntityUid uid, BlobOvermindComponent comp, EntityUid blobId)
    {
        var biomass = CountOwnedBiomass(blobId);
        var changed = false;
        while (biomass >= comp.NextEvolutionThreshold)
        {
            comp.EvolutionPoints++;
            comp.NextEvolutionThreshold += comp.EvolutionThresholdStep;
            changed = true;
        }

        if (!changed)
            return;

        _popup.PopupEntity(Loc.GetString("blob-action-evolution-gained", ("points", comp.EvolutionPoints)), uid, uid, PopupType.Small);
    }

    private void TryApplyUpgrade(EntityUid uid, BlobOvermindComponent comp, ref int level)
    {
        var cost = GetUpgradeCost(comp, level);

        if (comp.EvolutionPoints < cost)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-upgrade-insufficient-points", ("cost", cost), ("current", comp.EvolutionPoints)), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (level >= comp.MaxUpgradeLevel)
        {
            _popup.PopupEntity(Loc.GetString("blob-action-upgrade-maxed"), uid, uid, PopupType.SmallCaution);
            return;
        }

        level++;
        comp.EvolutionPoints -= cost;
        if (comp.BlobId is { } blobId)
        {
            comp.Resources = Math.Min(comp.Resources, GetEffectiveMaxResources(blobId, comp));
            SyncNetworkState(blobId, uid, comp);
        }
        else
        {
            UpdateBiomassAlert(uid, comp);
            Dirty(uid, comp);
        }

        _popup.PopupEntity(Loc.GetString("blob-action-upgrade-success", ("points", comp.EvolutionPoints)), uid, uid, PopupType.Small);
    }

    private void SyncStateFromExistingOvermind(EntityUid blobId, EntityUid uid, BlobOvermindComponent comp)
    {
        var query = EntityQueryEnumerator<BlobOvermindComponent>();
        while (query.MoveNext(out var otherUid, out var other))
        {
            if (otherUid == uid || other.BlobId != blobId)
                continue;

            comp.Resources = other.Resources;
            comp.ResourceAccumulator = other.ResourceAccumulator;
            comp.ResourceStructureAccumulator = other.ResourceStructureAccumulator;
            comp.EvolutionPoints = other.EvolutionPoints;
            comp.NextEvolutionThreshold = other.NextEvolutionThreshold;
            comp.GenerationUpgradeLevel = other.GenerationUpgradeLevel;
            comp.AttackUpgradeLevel = other.AttackUpgradeLevel;
            comp.CapacityUpgradeLevel = other.CapacityUpgradeLevel;
            comp.Chemical = other.Chemical;
            break;
        }

        UpdateBiomassAlert(uid, comp);
        Dirty(uid, comp);
    }

    private void SyncNetworkState(EntityUid blobId, EntityUid sourceUid, BlobOvermindComponent source)
    {
        var query = EntityQueryEnumerator<BlobOvermindComponent>();
        while (query.MoveNext(out var uid, out var overmind))
        {
            if (overmind.BlobId != blobId)
                continue;

            if (uid != sourceUid)
            {
                overmind.Resources = source.Resources;
                overmind.ResourceAccumulator = source.ResourceAccumulator;
                overmind.ResourceStructureAccumulator = source.ResourceStructureAccumulator;
                overmind.EvolutionPoints = source.EvolutionPoints;
                overmind.NextEvolutionThreshold = source.NextEvolutionThreshold;
                overmind.GenerationUpgradeLevel = source.GenerationUpgradeLevel;
                overmind.AttackUpgradeLevel = source.AttackUpgradeLevel;
                overmind.CapacityUpgradeLevel = source.CapacityUpgradeLevel;
                overmind.Chemical = source.Chemical;
            }

            ApplyChemicalVisual(uid, overmind.Chemical);
            UpdateBiomassAlert(uid, overmind);
            Dirty(uid, overmind);
        }
    }

    private BlobChemicalMenuState GetChemicalMenuState(BlobOvermindComponent comp)
    {
        return new BlobChemicalMenuState(
            comp.Chemical,
            BlobChemicalVisuals.SelectableChemicals.ToList(),
            comp.Resources,
            comp.ChemicalChangeCost,
            BlobChemicalVisuals.GetColor(comp.Chemical));
    }

    private static bool IsSelectableChemical(BlobChemicalType chemical)
    {
        return Array.IndexOf(BlobChemicalVisuals.SelectableChemicals, chemical) != -1;
    }

    private void ApplyChemicalVisualsForBlob(EntityUid blobId, BlobChemicalType chemical)
    {
        var structures = EntityQueryEnumerator<BlobStructureComponent>();
        while (structures.MoveNext(out var uid, out var structure))
        {
            if (structure.OwnerMind != blobId)
                continue;

            ApplyChemicalVisual(uid, chemical);
        }

        var overminds = EntityQueryEnumerator<BlobOvermindComponent>();
        while (overminds.MoveNext(out var uid, out var overmind))
        {
            if (overmind.BlobId != blobId)
                continue;

            ApplyChemicalVisual(uid, chemical);
        }
    }

    private void ApplyChemicalVisual(EntityUid uid, BlobChemicalType chemical)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        _appearance.SetData(uid, Content.Shared.Imperial.Blob.BlobVisuals.Color, BlobChemicalVisuals.GetColor(chemical), appearance);
    }

    private int GetPassiveIncome(BlobOvermindComponent comp)
    {
        return comp.PassiveIncome + comp.GenerationUpgradeLevel * comp.GenerationUpgradeBonus;
    }

    private float GetResourceTickInterval(BlobOvermindComponent comp)
    {
        return Math.Max(0.5f, comp.ResourceTickInterval - comp.GenerationUpgradeLevel * comp.GenerationTickIntervalReduction);
    }

    private bool TryBeginAttack(BlobOvermindComponent comp)
    {
        if (_timing.CurTime < comp.NextAttackTime)
            return false;

        comp.NextAttackTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ActiveAttackDelay);
        return true;
    }

    private bool TryBeginTileAction(BlobOvermindComponent comp)
    {
        if (_timing.CurTime < comp.NextTileActionTime)
            return false;

        comp.NextTileActionTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ActiveTileActionDelay);
        return true;
    }

    private bool IsFriendlyBlobEntity(EntityUid entity, EntityUid blobId)
    {
        if (TryComp<BlobStructureComponent>(entity, out var structure))
            return structure.OwnerMind == blobId;

        if (TryComp<BlobMobComponent>(entity, out var mob))
            return mob.OwnerMind == blobId;

        if (_npcFaction.IsMember(entity, BlobFactionId))
            return true;

        if (HasComp<BlobMouseComponent>(entity))
            return true;

        if (TryComp<BlobOvermindComponent>(entity, out var overmind))
            return overmind.BlobId == blobId;

        return false;
    }

    private int GetActiveAttackDamage(BlobOvermindComponent comp, int sources)
    {
        return comp.AttackBaseDamage + comp.AttackUpgradeLevel * comp.AttackUpgradeBonus + Math.Max(0, sources - 1) * comp.AttackBonusDamagePerSource;
    }

    private int GetAttackChemicalDamage(BlobOvermindComponent comp)
    {
        return comp.AttackChemicalDamage + comp.AttackUpgradeLevel * comp.AttackChemicalUpgradeBonus;
    }

    private int GetEffectiveMaxResources(EntityUid blobId, BlobOvermindComponent comp)
    {
        return comp.MaxResources + comp.CapacityUpgradeLevel * comp.CapacityUpgradeBonus + CountOwnedStructures(blobId, "BlobStorage") * comp.StorageMaxResourceBonus;
    }

    private int GetUpgradeCost(BlobOvermindComponent comp, int currentLevel)
    {
        return comp.UpgradeBaseCost + currentLevel * comp.UpgradeCostStep;
    }

    private float GetConsumeRefundRatio(BlobOvermindComponent comp)
    {
        return Math.Clamp(comp.ConsumeRefundRatio + comp.CapacityUpgradeLevel * comp.CapacityConsumeRefundBonus, 0f, 0.95f);
    }

    private bool TryGetConsumeRefund(string? prototype, BlobOvermindComponent comp, out int refund)
    {
        refund = 0;

        if (prototype == "BlobTile" || prototype == "BlobTileShield" || prototype == "BlobTileReflective")
            return true;

        var cost = prototype switch
        {
            "BlobNode" => comp.NodeCost,
            "BlobFactory" => comp.FactoryCost,
            "BlobResource" => comp.ResourceCost,
            "BlobStorage" => comp.StorageCost,
            "BlobLauncher" => comp.LauncherCost,
            "BlobCooling" => comp.CoolingCost,
            _ => 0,
        };

        if (cost <= 0)
            return false;

        refund = Math.Max(1, (int) MathF.Floor(cost * GetConsumeRefundRatio(comp)));
        return true;
    }

    private void UpdateBiomassAlert(EntityUid uid, BlobOvermindComponent comp)
    {
        var alerts = EnsureComp<AlertsComponent>(uid);
        var maxResources = comp.BlobId is { } blobId ? GetEffectiveMaxResources(blobId, comp) : comp.MaxResources;
        var severity = (short) Math.Clamp((int) MathF.Round(comp.Resources * 10f / Math.Max(1, maxResources)), 0, 10);
        _alerts.ShowAlert((uid, alerts), "BlobBiomass", severity);
    }

}