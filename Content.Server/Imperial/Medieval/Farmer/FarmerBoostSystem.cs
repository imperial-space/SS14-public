using System.Linq;
using Content.Server.Nutrition.Components;
using Content.Shared.Damage;
using Content.Shared.Hands;
using Content.Shared.Imperial.Medieval.Farmer;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.StatusEffect;

namespace Content.Server.Imperial.Medieval.Farmer;

public sealed class FarmerBoostSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public const string StatusEffectId = "MedievalFarmerBoost";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddFarmerBoostOnInitComponent, MapInitEvent>(OnMarkerInit);

        SubscribeLocalEvent<LastPickedUpContainerComponent, GotEquippedHandEvent>(OnContPickup);
        SubscribeLocalEvent<LastPickedUpContainerComponent, BeforeMicrowavedEvent>(OnContBeforeMicrowaved);

        SubscribeLocalEvent<FarmerComponent, UserAfterHarvestEvent>(OnFarmerHarvest);
        SubscribeLocalEvent<AfterMicrowavedEvent>(AfterMicrowawerCheck);

        SubscribeLocalEvent<FarmerBoostOnConsumeComponent, FullyEatenEvent>(OnBeforeFullyEaten);
    }

    private void OnMarkerInit(EntityUid uid, AddFarmerBoostOnInitComponent comp, MapInitEvent args)
        => _lookup.GetEntitiesInRange<FoodComponent>(Transform(uid).Coordinates, comp.Range).ToList()
                                                                   .ForEach(x => EnsureComp<FarmerBoostOnConsumeComponent>(x));

    private void OnContPickup(EntityUid uid, LastPickedUpContainerComponent comp, GotEquippedHandEvent args)
        => comp.Ent = args.User;

    private void OnContBeforeMicrowaved(EntityUid uid, LastPickedUpContainerComponent comp, ref BeforeMicrowavedEvent args)
    {
        if (comp.Ent.HasValue)
            args.Users.Add(comp.Ent.Value);
    }

    private void AfterMicrowawerCheck(ref AfterMicrowavedEvent args)
    {
        foreach (var item in args.Users)
        {
            if (!item.IsValid())
                continue;

            if (!HasComp<FarmerComponent>(item))
                return;
        }

        var comp = EnsureComp<FarmerBoostOnConsumeComponent>(args.Result);
        comp.Time = 15f;
    }

    private void OnFarmerHarvest(EntityUid uid, FarmerComponent comp, ref UserAfterHarvestEvent args)
    {
        var boost = EnsureComp<FarmerBoostOnConsumeComponent>(args.Harvested);
        boost.Time = 7;
    }

    private void OnBeforeFullyEaten(EntityUid uid, FarmerBoostOnConsumeComponent comp, FullyEatenEvent args)
        => _status.TryAddStatusEffect<FarmerBoostComponent>(args.User, StatusEffectId, TimeSpan.FromMinutes(comp.Time), true);
}
