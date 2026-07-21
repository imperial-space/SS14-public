using System.Numerics;
using Content.Server.Construction;
using Content.Server.Store.Components;
using Content.Shared._RD.Weight.Components;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Sprite;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Fishing;

public sealed partial class FishingSystem : EntitySystem
{
    public sealed class FishSizeRatity
    {
        public string Name = String.Empty;
        public float Chance = 0;
        public float PriceMod = 1;
        public float Scale = 1;
    }

    List<FishSizeRatity> _fishSizeRarities = new List<FishSizeRatity>
    {
        new FishSizeRatity { Name = "fish-size-tiny",     Chance = 15f,  PriceMod = 0.4f,  Scale = 0.25f },
        new FishSizeRatity { Name = "fish-size-small",    Chance = 20f,  PriceMod = 0.65f, Scale = 0.6f },
        new FishSizeRatity { Name = "fish-size-medium",   Chance = 30f,  PriceMod = 1.0f,  Scale = 1.0f },
        new FishSizeRatity { Name = "fish-size-large",    Chance = 18f,  PriceMod = 1.4f,  Scale = 1.5f },
        new FishSizeRatity { Name = "fish-size-huge",     Chance = 10f,  PriceMod = 1.9f,  Scale = 2.2f },
        new FishSizeRatity { Name = "fish-size-gigantic", Chance = 5f,   PriceMod = 2.8f,  Scale = 3.5f },
        new FishSizeRatity { Name = "fish-size-colossal", Chance = 1.5f, PriceMod = 5.0f,  Scale = 6.0f },
        new FishSizeRatity { Name = "fish-size-titanic",  Chance = 0.5f, PriceMod = 10.0f, Scale = 12.0f }
    };

    private FishSizeRatity GetRandomSize()
    {
        float totalChance = 0;
        for (int i = 0; i < _fishSizeRarities.Count; i++)
            totalChance += _fishSizeRarities[i].Chance;

        float roll = (float)new Random().NextDouble() * totalChance;
        for (int i = 0; i < _fishSizeRarities.Count; i++)
        {
            if (roll < _fishSizeRarities[i].Chance)
            {
                return _fishSizeRarities[i];
            }
            roll -= _fishSizeRarities[i].Chance;
        }

        return _fishSizeRarities[_fishSizeRarities.Count - 1];
    }

    private void GetFishRandomSizeRarity(EntityUid fishUid)
    {
        var rolledFishSizeRarity = GetRandomSize();

        var fishSizeRarity = EnsureComp<FishSizeRarityComponent>(fishUid);
        fishSizeRarity.Name = rolledFishSizeRarity.Name;
        fishSizeRarity.Chance = rolledFishSizeRarity.Chance;
        fishSizeRarity.PriceMod = rolledFishSizeRarity.PriceMod;
        fishSizeRarity.Scale = rolledFishSizeRarity.Scale;

        GetFishSizeRarityModificators((fishUid, fishSizeRarity));
    }

    private void GetFishSizeRarityModificators(Entity<FishSizeRarityComponent> entity)
    {
        string newName = Loc.GetString(entity.Comp.Name) + " " + Name(entity);
        _metaDataSystem.SetEntityName(entity, newName);

        var scale = new Vector2(MathF.Sqrt(entity.Comp.Scale), MathF.Sqrt(entity.Comp.Scale));
        _scaleVisuals.SetSpriteScale(entity, scale);

        if (TryComp<CurrencyComponent>(entity, out var currencyComp))
        {
            currencyComp.Price["Revent"] *= entity.Comp.PriceMod;
        }

        if (TryComp<MedievalCurrencyComponent>(entity, out var medievalCurrencyComponent))
        {
            medievalCurrencyComponent.Price["Revent"] *= entity.Comp.PriceMod;
        }

        if (TryComp<RDWeightComponent>(entity, out var rDWeightComp))
        {
            _rdWeight.ChangeWeightWithMod((entity, rDWeightComp), entity.Comp.Scale);
        }

        if (_solutionContainer.TryGetSolution(entity.Owner, "food", out _, out var solution))
        {
            solution.MaxVolume *= entity.Comp.Scale;
            solution.ScaleSolution(entity.Comp.Scale);
        }
    }

    private void OnScaleEntity(EntityUid fishUid, FishSizeRarityComponent component, ref ScaleEntityEvent args)
    {
        if (component.Fisher is null || component.Fisher == EntityUid.Invalid)
            return;

        var throwSpeed = GetFishThrowSpeed(fishUid);
    }

    private void OnFishTransformed(EntityUid uid, FishSizeRarityComponent comp, ref ConstructionChangeEntityEvent args)
    {
        if (!TryComp<FishSizeRarityComponent>(args.Old, out var oldSizeRarityComponent))
            return;

        comp.Name = oldSizeRarityComponent.Name;
        comp.Chance = oldSizeRarityComponent.Chance;
        comp.PriceMod = oldSizeRarityComponent.PriceMod;
        comp.Scale = oldSizeRarityComponent.Scale;
        if (uid == args.New)
            Timer.Spawn(0, () =>
            {
                GetFishSizeRarityModificators((uid, comp));
            });
    }
}

