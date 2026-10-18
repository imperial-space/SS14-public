using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing;
using Content.Shared.Imperial.Disease;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>
/// Передача болезней: воздух, кожа, жидкости, кровь (_MobProcs.dm, _disease.dm/airborne_spread).
/// </summary>
public sealed partial class DiseaseSystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private const string MaskSlot = "mask";
    private const string HeadSlot = "head";
    private const string OuterSlot = "outerClothing";
    private const string JumpsuitSlot = "jumpsuit";
    private const string GlovesSlot = "gloves";
    private const string ShoesSlot = "shoes";

    private void InitializeSpread()
    {
        SubscribeLocalEvent<DiseasedComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<DiseasedComponent, MeleeHitEvent>(OnDiseasedMeleeHit);
        SubscribeLocalEvent<DiseasedComponent, AttackedEvent>(OnDiseasedAttacked);
    }

    /// <summary>on_breath(): болезнь сама расходится по воздуху, пока носитель дышит.</summary>
    private void TrySpreadOnBreath(EntityUid uid, DiseaseInstance disease)
    {
        if ((disease.Spread & DiseaseSpread.Airborne) == 0)
            return;

        if (Prob(disease.Infectivity * 4))
            AirborneSpread(uid, disease);
    }

    /// <summary>airborne_spread(): заражает всех в радиусе, если между ними есть проход воздуха.</summary>
    public bool AirborneSpread(EntityUid uid, DiseaseInstance disease, float range = 2, bool force = true, bool requireFacing = false)
    {
        if ((disease.Spread & DiseaseSpread.Airborne) == 0 && !force)
            return false;

        if (!CanSpreadAirborne(uid))
            return false;

        if (!HasOrgan(uid, LungsCategory))
            return false;

        // Спейсациллин и сытость мешают разносить заразу.
        if (HasReagent(uid, Spaceacillin) || IsWellFed(uid) && _random.Prob(0.25f))
            return false;

        var source = Transform(uid);
        if (source.GridUid == null)
            return false;

        foreach (var target in _lookup.GetEntitiesInRange<BodyComponent>(_transform.GetMapCoordinates(uid), range))
        {
            if (target.Owner == uid || !HasComp<MobStateComponent>(target))
                continue;

            if (requireFacing && !IsFacing(uid, target))
                continue;

            if (!AirPathExists(uid, target))
                continue;

            ContractAirborne(target, disease);
        }

        return true;
    }

    /// <summary>contract_airborne_disease().</summary>
    public bool ContractAirborne(EntityUid uid, DiseaseInstance disease)
    {
        if (!CanBeSpreadAirborne(uid))
            return false;

        if (!_random.Prob(Math.Clamp(Math.Min(50 * disease.SpreadingModifier - 1, 50) / 100f, 0f, 1f)))
            return false;

        if (!HasOrgan(uid, LungsCategory))
            return false;

        return TryInfect(uid, disease);
    }

    /// <summary>can_spread_airborne_diseases(): маска и шлем задерживают заразу.</summary>
    private bool CanSpreadAirborne(EntityUid uid)
    {
        if (HasComp<DiseaseNoBreathComponent>(uid))
            return false;

        if (HasInternals(uid))
            return false;

        var protection = 2 * (GetSlotProtection(uid, HeadSlot) + GetSlotProtection(uid, MaskSlot));
        return !_random.Prob(Math.Clamp(protection / 100f, 0f, 1f));
    }

    /// <summary>can_be_spread_airborne_disease().</summary>
    private bool CanBeSpreadAirborne(EntityUid uid)
    {
        if (HasComp<DiseaseNoBreathComponent>(uid))
            return false;

        // Дыхание из баллона тоже защищает.
        if (HasInternals(uid) && _random.Prob(0.75f))
            return false;

        if (HasReagent(uid, Spaceacillin) && _random.Prob(0.75f))
            return false;

        var protection = GetSlotProtection(uid, HeadSlot) + GetSlotProtection(uid, MaskSlot);
        return !_random.Prob(Math.Clamp(protection / 100f, 0f, 1f));
    }

    /// <summary>ContactContractDisease(): заражение через прикосновение с учётом одежды на случайной части тела.</summary>
    public bool ContactContractDisease(EntityUid uid, DiseaseInstance disease)
    {
        if (!CanContract(uid, disease))
            return false;

        if (_random.Prob(Math.Clamp(15 / disease.SpreadingModifier / 100f, 0f, 1f)))
            return false;

        if (IsWellFed(uid) && _random.Prob(0.25f))
            return false;

        if (HasReagent(uid, Spaceacillin) && _random.Prob(0.75f))
            return false;

        // Зона попадания: туловище чаще всего, ноги реже всего.
        var zone = _random.Pick(new[]
        {
            (Zone: "head", Weight: 80f),
            (Zone: "chest", Weight: 100f),
            (Zone: "arm", Weight: 17.5f),
            (Zone: "arm2", Weight: 17.5f),
            (Zone: "leg", Weight: 7.5f),
            (Zone: "leg2", Weight: 7.5f),
        }.SelectMany(x => Enumerable.Repeat(x.Zone, (int) (x.Weight * 2))).ToList());

        var passed = true;
        switch (zone)
        {
            case "head":
                passed = PassesSlot(uid, HeadSlot) && PassesSlot(uid, MaskSlot);
                break;
            case "chest":
                passed = PassesSlot(uid, OuterSlot) && PassesSlot(uid, JumpsuitSlot);
                break;
            case "arm":
            case "arm2":
                passed = PassesSlot(uid, OuterSlot) && PassesSlot(uid, GlovesSlot);
                break;
            default:
                passed = PassesSlot(uid, OuterSlot) && PassesSlot(uid, ShoesSlot);
                break;
        }

        return passed && TryInfect(uid, disease);
    }

    private bool PassesSlot(EntityUid uid, string slot)
    {
        return !_random.Prob(Math.Clamp(GetSlotProtection(uid, slot) / 100f, 0f, 1f));
    }

    /// <summary>
    /// Защита слота от заразы. В SS13 это броня BIO; здесь — герметичная одежда,
    /// закрытый шлем и защита от давления.
    /// </summary>
    private float GetSlotProtection(EntityUid uid, string slot)
    {
        if (!_inventory.TryGetSlotEntity(uid, slot, out var item))
            return 0;

        var protection = 0f;
        if (HasComp<Content.Server.Atmos.Components.PressureProtectionComponent>(item))
            protection += 75;

        if (HasComp<Content.Shared.Nutrition.Components.IngestionBlockerComponent>(item))
            protection += 50;

        if (HasComp<Content.Shared.Clothing.Components.MaskComponent>(item))
            protection += 25;

        return Math.Min(protection, 100);
    }

    private bool HasInternals(EntityUid uid)
    {
        return TryComp<Content.Shared.Body.Components.InternalsComponent>(uid, out var internals)
               && internals.GasTankEntity != null;
    }

    /// <summary>disease_air_spread_walk(): воздух не проходит сквозь стены.</summary>
    private bool AirPathExists(EntityUid from, EntityUid to)
    {
        var start = Transform(from);
        var end = Transform(to);
        if (start.GridUid is not { } grid || end.GridUid != grid
            || !TryComp<Robust.Shared.Map.Components.MapGridComponent>(grid, out var gridComp))
            return false;

        var startTile = _map.TileIndicesFor(grid, gridComp, start.Coordinates);
        var endTile = _map.TileIndicesFor(grid, gridComp, end.Coordinates);

        var current = endTile;
        for (var i = 0; i < 16 && current != startTile; i++)
        {
            var step = new Vector2i(Math.Sign(startTile.X - current.X), Math.Sign(startTile.Y - current.Y));
            var next = current + step;
            if (!_map.TryGetTileRef(grid, gridComp, next, out var tile) || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                return false;

            current = next;
        }

        return true;
    }

    private bool IsFacing(EntityUid source, EntityUid target)
    {
        var dir = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(source);
        if (dir.LengthSquared() < 0.01f)
            return true;

        return dir.ToWorldAngle().GetDir() == Transform(source).LocalRotation.GetDir();
    }

    #region Контакт

    private void OnInteractHand(Entity<DiseasedComponent> ent, ref InteractHandEvent args)
    {
        SpreadByContact(ent, args.User);
    }

    private void OnDiseasedMeleeHit(Entity<DiseasedComponent> ent, ref MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            SpreadByContact(ent, target);
        }
    }

    private void OnDiseasedAttacked(Entity<DiseasedComponent> ent, ref AttackedEvent args)
    {
        SpreadByContact(ent, args.User);
    }

    private void SpreadByContact(Entity<DiseasedComponent> ent, EntityUid target)
    {
        if (target == ent.Owner || !HasComp<BodyComponent>(target))
            return;

        foreach (var disease in ent.Comp.Diseases.ToArray())
        {
            if ((disease.Spread & DiseaseSpread.ContactSkin) == 0)
                continue;

            ContactContractDisease(target, disease);
        }
    }

    #endregion

    private static readonly ProtoId<OrganCategoryPrototype> LungsCategory = "Lungs";
}
