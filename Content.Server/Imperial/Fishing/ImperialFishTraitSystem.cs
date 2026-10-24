using System.Linq;
using Content.Shared.Imperial.Fishing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Черты рыб (/datum/fish_trait): влияние на сложность, вес в таблице улова и правила мини-игры.
/// </summary>
public sealed partial class ImperialFishTraitSystem : EntitySystem
{
    [Dependency] private readonly ImperialFishingBaitSystem _bait = default!;
    [Dependency] private readonly ImperialFishDataSystem _data = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    /// <summary>PAYCHECK_CREW: сколько денег должно быть на крючке для любителей блестящего.</summary>
    private const int PaycheckCrew = 50;

    public override void Initialize()
    {
        base.Initialize();
        InitializeLife();
    }

    /// <summary>difficulty_mod(): (прибавка, множитель).</summary>
    public (float Add, float Mult) DifficultyMod(string trait, ImperialFishingRodInfo rod, EntityUid user)
    {
        var add = 0f;
        switch (trait)
        {
            case "Wary":
                if (!rod.LineHas(ImperialFishingLineTraits.Cloaked))
                    add += ImperialFishing.TraitMinorDifficultyBoost;
                break;
            case "ShinyLover":
                if (rod.HookComp?.AttractShiny == true || AttractsShiny(rod.Bait)
                    || rod.Bait is { } bait && _bait.GetCreditValue(bait) >= PaycheckCrew)
                    add -= ImperialFishing.TraitMinorDifficultyBoost;
                break;
            case "Anxiety":
                if (rod.LineHas(ImperialFishingLineTraits.Cloaked))
                    add -= ImperialFishing.TraitMinorDifficultyBoost;
                break;
        }

        return (add, 1f);
    }

    private bool AttractsShiny(EntityUid? bait)
    {
        return bait != null && CompOrNull<ImperialFishingBaitComponent>(bait)?.Lure == ImperialLureKind.LuckyCoin;
    }

    private bool IgnoresEnvironment(ImperialFishingRodInfo rod)
    {
        return rod.Bait != null && CompOrNull<ImperialFishingBaitComponent>(rod.Bait)?.Lure == ImperialLureKind.Led;
    }

    /// <summary>catch_weight_mod(): (прибавка, множитель).</summary>
    public (float Add, float Mult) CatchWeightMod(string trait, ImperialFishingRodInfo rod, EntityUid user, EntityUid location, string fishProto)
    {
        var mult = 1f;
        var bait = rod.Bait;
        var fish = _data.GetFish(fishProto);

        switch (trait)
        {
            case "ShinyLover":
                if (rod.HookComp?.AttractShiny != true && !AttractsShiny(bait))
                    mult = 0.5f;
                break;

            case "PickyEater":
            {
                if (fish == null)
                    break;

                if (bait == null)
                {
                    mult = 0;
                    break;
                }

                if (_bait.IsOmni(bait.Value))
                    break;

                if (fish.FavoriteBait.Any(id => _bait.Matches(bait.Value, id)))
                    break;

                if (fish.DislikedBait.Any(id => _bait.Matches(bait.Value, id)))
                {
                    mult = 0;
                    break;
                }

                if (_bait.GetQuality(bait.Value) is not (ImperialBaitQuality.Good or ImperialBaitQuality.Great))
                    mult = 0;
                break;
            }

            case "Nocturnal":
                if (!IgnoresEnvironment(rod) && IsLit(Transform(location).Coordinates))
                    mult = 0;
                break;

            case "Carnivore":
                if (bait == null)
                {
                    mult = 0;
                    break;
                }

                if (_bait.IsOmni(bait.Value))
                    break;

                if ((_bait.GetFoodTypes(bait.Value) & (ImperialFoodTypes.Meat | ImperialFoodTypes.Seafood | ImperialFoodTypes.Bugs)) == 0)
                    mult = 0;
                break;

            case "Vegan":
            {
                if (bait == null)
                {
                    mult = 0;
                    break;
                }

                if (_bait.IsOmni(bait.Value))
                    break;

                var types = _bait.GetFoodTypes(bait.Value);
                var liked = (types & (ImperialFoodTypes.Vegetables | ImperialFoodTypes.Fruit)) != 0;
                var hated = (types & (ImperialFoodTypes.Meat | ImperialFoodTypes.Seafood | ImperialFoodTypes.Gore | ImperialFoodTypes.Bugs | ImperialFoodTypes.Dairy)) != 0;
                if (!liked || hated)
                    mult = 0;
                break;
            }

            case "Predator":
                if (bait != null && HasComp<ImperialFishComponent>(bait))
                    mult *= 2;
                break;
        }

        return (0f, mult);
    }

    /// <summary>minigame_mod().</summary>
    public void MinigameMod(string trait, ImperialFishingChallenge challenge)
    {
        switch (trait)
        {
            case "Heavy":
                if (challenge.Mover != null)
                    challenge.Mover.FishIdleVelocity -= 10;
                break;
            case "Lubed":
                challenge.ReelingVelocity *= 1.4f;
                challenge.GravityVelocity *= 1.4f;
                break;
            case "Antigrav":
                challenge.SpecialEffects |= ImperialFishingRules.Antigrav;
                break;
            case "Camouflage":
                challenge.SpecialEffects |= ImperialFishingRules.Camo;
                break;
        }
    }

    /// <summary>
    /// get_lumcount() > SHADOW_SPECIES_LIGHT_THRESHOLD: на сервере освещения нет,
    /// поэтому считаем освещённым место в радиусе действия включённого источника света.
    /// </summary>
    public bool IsLit(EntityCoordinates coords)
    {
        var mapCoords = _transform.ToMapCoordinates(coords);
        foreach (var light in _lookup.GetEntitiesInRange<PointLightComponent>(mapCoords, 10f))
        {
            if (!light.Comp.Enabled || light.Comp.Energy <= 0.1f)
                continue;

            var lightPos = _transform.GetMapCoordinates(light.Owner);
            if ((lightPos.Position - mapCoords.Position).Length() <= light.Comp.Radius * 0.6f)
                return true;
        }

        return false;
    }
}
