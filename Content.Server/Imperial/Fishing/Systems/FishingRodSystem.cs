using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Server.Power.Components;
using Robust.Shared.Containers;
using Content.Shared.Imperial.Fishing.FishingRodComponents;
using Robust.Shared.Map;
using Content.Shared.Imperial.Fishing.Actions;
using Content.Shared.Actions;
using Robust.Shared.Random;
using Content.Server.Administration.Logs;
using Content.Shared.Hands.EntitySystems;
using static Content.Shared.Storage.EntitySpawnCollection;
using Content.Shared.Imperial.Fishing.Enums;

namespace Content.Server.Imperial.Fishing.FishingRodSystem;

public sealed class FishingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishingRodComponent, GetItemActionsEvent>(GetStartAction);
        SubscribeLocalEvent<FishingRodComponent, FishingStartAction>(OnStartFishing);
        SubscribeLocalEvent<FishingRodComponent, FishingEndAction>(OnEndFishing);
    }

    private void GetStartAction(EntityUid uid, FishingRodComponent component, GetItemActionsEvent args)
    {
        args.AddAction(ref component.StartActionEntity, component.StartAction);
        args.AddAction(ref component.EndActionEntity, component.EndAction);
    }
    public override void Update(float frameTime)
    {
        var queryOne = EntityQueryEnumerator<FishingRodComponent>();
        while (queryOne.MoveNext(out var body, out var component))
        {
            if (!component.IsFishing)
                continue;
            component.AccumulatorVisual += frameTime;
            var coordsRod = Transform(body).Coordinates;
            if (!_transform.InRange(component.CoordsTarget, coordsRod, component.FishingDistance))
            {
                _popup.PopupPredicted(Loc.GetString("line-broke-fishing-message"), body, body, type: PopupType.MediumCaution);
                component.IsFishing = false;
                component.IsDistanceNormal = false;
                EndFishing(body, component);
                break;
            }
            if (component.AccumulatorVisual >= component.VisualFishingTime && component.PopupCaution)
            {
                _popup.PopupPredicted(Loc.GetString("Peck-fishing-message"), body, body, type: PopupType.MediumCaution);
                component.PopupCaution = false;
            }
            if (component.AccumulatorVisual >= component.FishingTime && component.IsFishing == true)
            {
                component.AccumulatorVisual = 0;
                component.IsFishing = false;
                EndFishing(body, component);
            }
        }
        base.Update(frameTime);
    }

    private void OnStartFishing(EntityUid uid, FishingRodComponent component, FishingStartAction args)
    {
        var containerSlotOne = _container.GetContainer(uid, component.UpgradesContainerIdOne);
        var containerSlotTwo = _container.GetContainer(uid, component.UpgradesContainerIdTwo);
        var totalUpgrades = containerSlotOne.ContainedEntities.Count + containerSlotTwo.ContainedEntities.Count;
        component.FishingRodUid = uid;
        component.CoordsTarget = args.Target;
        component.CoordsRod = Transform(uid).Coordinates;
        if (!IsPositionValid(component.CoordsTarget, component))
            return;
        if (component.IsFishing)
            return;
        if (args.Handled)
            return;
        if (!_transform.InRange(component.CoordsTarget, component.CoordsRod, component.FishingDistance))
            return;
        if (totalUpgrades > component.MaxUpgradeCount)
            return;
        component.User = args.Performer;

        component.IsFishing = true;
        var fishingFloat = SpawnAttachedTo(component.Float, component.CoordsTarget);
        Transform(fishingFloat).LocalRotation = Angle.Zero;
        component.FloatEntity = fishingFloat;


        foreach (var contained in containerSlotOne.ContainedEntities)
        {
            if (TryComp<FishingRodUpgraderComponent>(contained, out var upgradeComp))
            {
                component.FishingReelCoefficient = upgradeComp.ReelCoefficent;
                component.FishingHookCoefficient = upgradeComp.HookCoefficent;
            }
            if (containerSlotOne.ContainedEntities.Count == 0)
            {
                component.FishingReelCoefficient = component.BaseCoefficent;
                component.FishingHookCoefficient = component.BaseCoefficent;
            }
        }
        foreach (var contained in containerSlotTwo.ContainedEntities)
        {
            if (TryComp<FishingRodUpgraderComponent>(contained, out var upgradeComp))
            {
                component.FishingReelCoefficient = upgradeComp.ReelCoefficent;
                component.FishingHookCoefficient = upgradeComp.HookCoefficent;
            }
            if (containerSlotTwo.ContainedEntities.Count == 0)
            {
                component.FishingReelCoefficient = component.BaseCoefficent;
                component.FishingHookCoefficient = component.BaseCoefficent;
            }
        }

        var fishingTime = _random.NextFloat(component.MinFishingTime, component.MaxFishingTime);
        component.FishingTime = fishingTime / component.FishingReelCoefficient;
        component.VisualFishingTime = component.FishingTime - component.IntervalVisualFishingTime - component.FishingHookCoefficient;
        args.Handled = true;
    }

    private void OnEndFishing(EntityUid uid, FishingRodComponent component, FishingEndAction args)
    {
        if (!component.IsFishing) return;
        component.IsFishing = false;
        if (component.FishingTime - component.AccumulatorVisual <= component.IntervalVisualFishingTime)
            GetItem(uid, component);
        EndFishing(uid, component);
    }
    private void EndFishing(EntityUid uid, FishingRodComponent component)
    {
        if (!component.IsFishing)
        {
            if (component.IsDistanceNormal)
                _popup.PopupPredicted(Loc.GetString("End-fishing-message"), uid, uid, type: PopupType.MediumCaution);
            component.PopupCaution = true;
            component.AccumulatorVisual = 0f;
            component.IsDistanceNormal = true;
            _entMan.DeleteEntity(component.FloatEntity);
            component.Place = PlaceID.None;
        }
    }
    private void GetItem(EntityUid fishingRod, FishingRodComponent component)
    {
        if (!IsPositionValid(component.CoordsTarget, component))
            return;
        var coords = Transform(component.FishingRodUid).Coordinates;
        var spawnEntitiesWater = GetSpawns(component.WaterItems, _random);
        var spawnEntitiesLava = GetSpawns(component.LavaItems, _random);
        var spawnEntitiesVoid = GetSpawns(component.VoidItems, _random);
        var spawnEntitiesDebug = GetSpawns(component.DebugItems, _random);
        EntityUid entityToPlaceInHands;
        if (component.FishingWater && component.Place is PlaceID.FishingPortalID) // FishingPortal
            foreach (var proto in spawnEntitiesWater)
            {
                entityToPlaceInHands = SpawnAttachedTo(proto, coords);
                _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low, $"{ToPrettyString(component.User)} used {ToPrettyString(fishingRod)} which spawned {ToPrettyString(entityToPlaceInHands)}");
                _hands.PickupOrDrop(component.User, entityToPlaceInHands);
            }
        if (component.FishingWater && component.Place is PlaceID.WaterID)
            foreach (var proto in spawnEntitiesWater)
            {
                entityToPlaceInHands = SpawnAttachedTo(proto, coords);
                _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low, $"{ToPrettyString(component.User)} used {ToPrettyString(fishingRod)} which spawned {ToPrettyString(entityToPlaceInHands)}");
                _hands.PickupOrDrop(component.User, entityToPlaceInHands);
            }
        if (component.FishingLava && component.Place is PlaceID.LavaID)
            foreach (var proto in spawnEntitiesLava)
            {
                entityToPlaceInHands = SpawnAttachedTo(proto, coords);
                _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low, $"{ToPrettyString(component.User)} used {ToPrettyString(fishingRod)} which spawned {ToPrettyString(entityToPlaceInHands)}");
                _hands.PickupOrDrop(component.User, entityToPlaceInHands);
            }
        if (component.FishingVoid && component.Place is PlaceID.VoidID)
            foreach (var proto in spawnEntitiesVoid)
            {
                entityToPlaceInHands = SpawnAttachedTo(proto, coords);
                _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low, $"{ToPrettyString(component.User)} used {ToPrettyString(fishingRod)} which spawned {ToPrettyString(entityToPlaceInHands)}");
                _hands.PickupOrDrop(component.User, entityToPlaceInHands);
            }
        if (component.FishingDebug || component.FishingDebug && component.Place is PlaceID.DebugID)
            foreach (var proto in spawnEntitiesDebug)
            {
                entityToPlaceInHands = SpawnAttachedTo(proto, coords);
                _adminLogger.Add(LogType.EntitySpawn, LogImpact.Low, $"{ToPrettyString(component.User)} used {ToPrettyString(fishingRod)} which spawned {ToPrettyString(entityToPlaceInHands)}");
                _hands.PickupOrDrop(component.User, entityToPlaceInHands);
            }
    }
    private bool IsPositionValid(EntityCoordinates coords, FishingRodComponent component)
    {
        foreach (var entity in _entityLookup.GetEntitiesInRange(coords, 0.001f))
            if (CheckComponent(entity, component))
            {
                return true;
            }
        return false;
    }
    private bool CheckComponent(EntityUid uid, FishingRodComponent component)
    {
        AllowedFishingComponent allowedFishingComp;
        if (HasComp<AllowedFishingComponent>(uid))
        {
            allowedFishingComp = Comp<AllowedFishingComponent>(uid);
            if (allowedFishingComp.FishingWater)
            {
                if (component.FishingWater)
                {
                    component.Place = PlaceID.WaterID;
                    if (TryComp<ApcPowerReceiverComponent>(uid, out var power) && !power.Powered)
                        return false;
                    return true;
                }
                if (component.FishingDebug)
                {
                    component.Place = PlaceID.DebugID;
                    return true;
                }
                return false;
            }
            if (allowedFishingComp.FishingLava)
            {
                if (component.FishingLava)
                {
                    component.Place = PlaceID.LavaID;
                    return true;
                }
                if (component.FishingDebug)
                {
                    component.Place = PlaceID.DebugID;
                    return true;
                }
                return false;
            }
            if (allowedFishingComp.FishingVoid)
            {
                if (component.FishingVoid)
                {
                    component.Place = PlaceID.VoidID;
                    return true;
                }
                if (component.FishingDebug)
                {
                    component.Place = PlaceID.DebugID;
                    return true;
                }
                return false;
            }
            if (component.FishingDebug)
            {
                component.Place = PlaceID.DebugID;
                return true;
            }
            else
                return false;
        }
        else
            return false;
    }
}
