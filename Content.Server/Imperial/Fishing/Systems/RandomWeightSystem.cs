using Content.Shared.Examine;
using Content.Shared.Imperial.Fishing.RandomWeightComponentComponents;
using Robust.Shared.Random;
using System.Numerics;
using Content.Shared.Sprite;
using Content.Server.Cargo.Components;

namespace Content.Shared.Imperial.Fishing.RandomWeightSystem;

public sealed class RandomWeightSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RandomWeightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RandomWeightComponent, ExaminedEvent>(ItemExamined);
    }
    private void OnMapInit(Entity<RandomWeightComponent> entity, ref MapInitEvent args)
    {
        entity.Comp.Weight = _random.NextFloat(entity.Comp.MinWeight, entity.Comp.MaxWeight);
        Vector2 scale = new Vector2(entity.Comp.Weight, entity.Comp.Weight);
        _sprite.SetSpriteScale(entity, scale);
        var priceComp = Comp<StaticPriceComponent>(entity);
        priceComp.Price = (int)(entity.Comp.BasePrice * entity.Comp.Weight * entity.Comp.PriceCoefficent);
    }
    private void ItemExamined(EntityUid uid, RandomWeightComponent component, ExaminedEvent args)
    {
        var weight = component.Weight;
        args.PushMarkup(Loc.GetString($"random-weight-is", ("weight", weight)));
    }
}
