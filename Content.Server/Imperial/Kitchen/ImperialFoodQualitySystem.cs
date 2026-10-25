using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.Mood;
using Content.Server.Imperial.Quirks;
using Content.Shared.Medical;
using Content.Shared.Alert;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Kitchen;
using Content.Shared.Imperial.Quirks;
using Content.Shared.Kitchen;
using Content.Server.Kitchen.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Kitchen;

/// <summary>TRAIT_HANDMADE: блюдо приготовил игрок, его качество — сложность рецепта.</summary>
[RegisterComponent]
public sealed partial class ImperialHandmadeComponent : Component
{
    [DataField]
    public int Complexity;

    /// <summary>bitecount == 0: бафф даёт только первый укус.</summary>
    public bool Bitten;
}

/// <summary>/datum/status_effect/food: бафф от блюда, сделанного руками.</summary>
[RegisterComponent]
public sealed partial class ImperialFoodBuffComponent : Component
{
    public int Strength;
    public TimeSpan End;
}

/// <summary>Когда существо в последний раз оценивало блюдо (last_check_time, 5 секунд).</summary>
[RegisterComponent]
public sealed partial class ImperialFoodTasterComponent : Component
{
    public Dictionary<EntityUid, TimeSpan> LastChecks = new();
}

/// <summary>
/// edible/checkLiked TG: качество блюда = сложность рукотворного рецепта ± вкусы вида,
/// напитки дают настроение по quality реагентов, рукотворная еда даёт бафф.
/// </summary>
public sealed class ImperialFoodQualitySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly ImperialMoodSystem _mood = default!;
    [Dependency] private readonly ImperialQuirkSystem _quirks = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly RecipeManager _recipes = default!;

    private static readonly ProtoId<AlertPrototype> BuffAlert = "ImperialFoodBuff";
    private static readonly ProtoId<ImperialSpeciesTastePrototype> DefaultTaste = "Default";

    /// <summary>STOP_SERVING_BREAKFAST.</summary>
    private static readonly TimeSpan BreakfastTime = TimeSpan.FromMinutes(15);

    /// <summary>Длительность настроения и баффа: 5 минут × 2 (чистота реагентов SS14 всегда полная).</summary>
    private const float TimeoutMod = 2f;

    private static readonly TimeSpan BuffDuration = TimeSpan.FromMinutes(5);

    private readonly HashSet<EntityUid> _cooking = new();

    /// <summary>Причуды, меняющие вкусы (те же, что в ImperialQuirkSystem).</summary>
    private static readonly Dictionary<string, (ImperialFoodTypes Liked, ImperialFoodTypes Disliked, bool Swap)> TasteQuirks = new()
    {
        ["ImperialQuirkVegetarian"] = (ImperialFoodTypes.None, ImperialFoodTypes.Meat, false),
        ["ImperialQuirkPineappleHater"] = (ImperialFoodTypes.None, ImperialFoodTypes.Pineapple, false),
        ["ImperialQuirkPineappleLiker"] = (ImperialFoodTypes.Pineapple, ImperialFoodTypes.None, false),
        ["ImperialQuirkDeviantTastes"] = (ImperialFoodTypes.None, ImperialFoodTypes.None, true),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialMoodComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<ImperialFoodBuffComponent, RefreshMovementSpeedModifiersEvent>(OnBuffSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        TrackMicrowaves();

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialFoodBuffComponent>();
        while (query.MoveNext(out var uid, out var buff))
        {
            if (now < buff.End)
                continue;

            _alerts.ClearAlert(uid, BuffAlert);
            RemCompDeferred<ImperialFoodBuffComponent>(uid);
            _speed.RefreshMovementSpeedModifiers(uid);
        }
    }

    #region Рукотворные блюда

    /// <summary>microwavable: то, что выдала закончившая готовку микроволновка, сделано руками.</summary>
    private void TrackMicrowaves()
    {
        var active = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<ActiveMicrowaveComponent>();
        while (query.MoveNext(out var uid, out _))
            active.Add(uid);

        foreach (var microwave in _cooking)
        {
            if (active.Contains(microwave) || !Exists(microwave))
                continue;

            var tick = _timing.CurTick.Value;
            foreach (var food in _lookup.GetEntitiesInRange<EdibleComponent>(Transform(microwave).Coordinates, 0.5f))
            {
                if (tick - MetaData(food).CreationTick.Value <= 2)
                    MarkHandmade(food);
            }
        }

        _cooking.Clear();
        _cooking.UnionWith(active);
    }

    /// <summary>ADD_TRAIT(TRAIT_HANDMADE).</summary>
    public void MarkHandmade(EntityUid food)
    {
        EnsureComp<ImperialHandmadeComponent>(food).Complexity = GetComplexity(food);
    }

    /// <summary>crafting_complexity TG; для блюд без пары в TG — по числу ингредиентов рецепта микроволновки.</summary>
    public int GetComplexity(EntityUid food)
    {
        if (GetData(food) is { } data)
            return data.Complexity;

        if (MetaData(food).EntityPrototype?.ID is not { } id)
            return 0;

        foreach (var recipe in _recipes.Recipes)
        {
            if (recipe.Result != id)
                continue;

            var ingredients = recipe.IngredientsSolids.Count + recipe.IngredientsReagents.Count;
            return Math.Clamp(ingredients - 1, 1, 5);
        }

        return 0;
    }

    public ImperialFoodDataPrototype? GetData(EntityUid food)
    {
        if (MetaData(food).EntityPrototype is not { } proto)
            return null;

        foreach (var (id, _) in _proto.EnumerateAllParents<EntityPrototype>(proto.ID, true))
        {
            if (_proto.TryIndex<ImperialFoodDataPrototype>(id, out var data))
                return data;
        }

        return null;
    }

    /// <summary>foodtypes блюда: из TG, иначе по вкусам, тегам и реагентам.</summary>
    public ImperialFoodTypes GetFoodTypes(EntityUid food, Content.Shared.Chemistry.Components.Solution? split)
    {
        if (GetData(food) is { } data)
            return ImperialKitchen.Combine(data.Types);

        var result = ImperialFoodTypes.None;
        var guessed = _quirks.GetFoodType(food, split);
        foreach (ImperialFoodType flag in Enum.GetValues(typeof(ImperialFoodType)))
        {
            if (flag != ImperialFoodType.None && guessed.HasFlag(flag) && Enum.TryParse<ImperialFoodTypes>(flag.ToString(), out var mapped))
                result |= mapped;
        }

        return result;
    }

    #endregion

    #region Вкус

    /// <summary>Вкусы языка вида с поправками причуд.</summary>
    private (ImperialFoodTypes Liked, ImperialFoodTypes Disliked, ImperialFoodTypes Toxic) GetTastes(EntityUid eater)
    {
        ImperialSpeciesTastePrototype? taste = null;
        if (TryComp<HumanoidProfileComponent>(eater, out var profile))
            _proto.TryIndex<ImperialSpeciesTastePrototype>(profile.Species.Id, out taste);
        taste ??= _proto.Index(DefaultTaste);

        var liked = ImperialKitchen.Combine(taste.Liked);
        var disliked = ImperialKitchen.Combine(taste.Disliked);
        var toxic = ImperialKitchen.Combine(taste.Toxic);

        if (TryComp<ImperialQuirksComponent>(eater, out var quirks))
        {
            foreach (var quirk in quirks.Quirks)
            {
                if (!TasteQuirks.TryGetValue(quirk.Id, out var mod))
                    continue;

                if (mod.Swap)
                    (liked, disliked) = (disliked, liked);
                liked = (liked | mod.Liked) & ~mod.Disliked;
                disliked = (disliked | mod.Disliked) & ~mod.Liked;
            }
        }

        return (liked, disliked, toxic);
    }

    /// <summary>get_perceived_food_quality.</summary>
    public int GetPerceivedQuality(EntityUid eater, EntityUid food, ImperialFoodTypes types)
    {
        var quality = TryComp<ImperialHandmadeComponent>(food, out var handmade) ? handmade.Complexity : 0;
        if (!HasComp<HumanoidProfileComponent>(eater))
            return Math.Min(quality, ImperialKitchen.QualityTop);

        var (liked, disliked, toxic) = GetTastes(eater);
        if (ImperialKitchen.Count(types, toxic) > 0)
            return ImperialKitchen.ToxicThreshold;

        if (HasComp<ImperialQuirkAgeusiaComponent>(eater))
            return 0;

        quality += ImperialKitchen.DislikedChange * ImperialKitchen.Count(types, disliked);
        quality += ImperialKitchen.LikedChange * ImperialKitchen.Count(types, liked);
        return Math.Min(quality, ImperialKitchen.QualityTop);
    }

    private void OnIngesting(Entity<ImperialMoodComponent> ent, ref IngestingEvent args)
    {
        var food = args.Food;
        if (!TryComp<EdibleComponent>(food, out var edible))
            return;

        DrinkQuality(ent, args);

        if (edible.Edible != IngestionSystem.Food)
            return;

        // Бафф только за первый укус рукотворного блюда.
        if (TryComp<ImperialHandmadeComponent>(food, out var handmade) && !handmade.Bitten)
        {
            handmade.Bitten = true;
            ApplyBuff(ent, handmade.Complexity);
        }

        var taster = EnsureComp<ImperialFoodTasterComponent>(ent);
        var now = _timing.CurTime;
        if (taster.LastChecks.TryGetValue(food, out var last) && now - last < TimeSpan.FromSeconds(5))
            return;

        taster.LastChecks[food] = now;
        CheckLiked(ent, food, args.Split);
    }

    /// <summary>checkLiked.</summary>
    private void CheckLiked(Entity<ImperialMoodComponent> eater, EntityUid food, Content.Shared.Chemistry.Components.Solution split)
    {
        var types = GetFoodTypes(food, split);
        if (types.HasFlag(ImperialFoodTypes.Breakfast) && _ticker.RoundDuration() < BreakfastTime)
            _mood.AddMood(eater.AsNullable(), "breakfast", "breakfast");

        var quality = GetPerceivedQuality(eater, food, types);
        var strong = HasComp<ImperialQuirkStrongStomachComponent>(eater);

        if (quality <= ImperialKitchen.ToxicThreshold)
        {
            // adjust_disgust(25 + 30 * fraction): отвращения в SS14 нет — тошнит.
            _popup.PopupEntity(Loc.GetString("imperial-food-toxic"), eater, eater, PopupType.MediumCaution);
            _mood.AddMood(eater.AsNullable(), "toxic_food", "disgusting_food");
            if (!strong && _random.Prob(0.5f))
                _vomit.Vomit(eater);
            return;
        }

        if (quality < 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-food-gross"), eater, eater, PopupType.SmallCaution);
            _mood.AddMood(eater.AsNullable(), "gross_food", "gross_food");
            if (!strong && _random.Prob(0.2f))
                _vomit.Vomit(eater);
            return;
        }

        if (quality == 0)
            return;

        // /datum/mood_event/food: 1 + 1.5 × качество, аскету вдвое меньше, гурману обычная еда — как первый уровень.
        var personality = eater.Comp.Personalities;
        float change = 1 + 1.5f * quality;
        if (personality.Contains("ascetic"))
            change *= 0.5f;
        if (personality.Contains("gourmand") && change <= 3)
            change = ImperialKitchen.QualityNormal;

        var label = Loc.GetString($"imperial-food-quality-{Math.Clamp(quality, 1, ImperialKitchen.QualityTop)}");
        var existing = _mood.GetMood(eater.AsNullable(), "quality_food");
        var value = (int) Math.Ceiling(change);
        if (existing == null || existing.Change <= value)
        {
            _mood.ClearMood(eater.AsNullable(), "quality_food");
            _mood.AddMood(eater.AsNullable(), "quality_food", "food", change: value,
                description: Loc.GetString("imperial-food-quality-mood", ("quality", label)), timeout: 300 * TimeoutMod);
        }

        _popup.PopupEntity(Loc.GetString("imperial-food-quality-popup", ("quality", label)), eater, eater);
    }

    /// <summary>consumable/expose_mob(INGEST): напиток с quality даёт настроение.</summary>
    private void DrinkQuality(Entity<ImperialMoodComponent> ent, IngestingEvent args)
    {
        if (HasComp<ImperialQuirkAgeusiaComponent>(ent))
            return;

        var best = 0;
        foreach (var (reagent, _) in args.Split.Contents)
        {
            if (_proto.TryIndex<ImperialDrinkQualityPrototype>(reagent.Prototype, out var quality))
                best = Math.Max(best, quality.Quality);
        }

        switch (best)
        {
            case 1: _mood.AddMood(ent.AsNullable(), "quality_drink", "quality_revolting"); break;
            case 2: _mood.AddMood(ent.AsNullable(), "quality_drink", "quality_nice"); break;
            case 3: _mood.AddMood(ent.AsNullable(), "quality_drink", "quality_good"); break;
            case 4: _mood.AddMood(ent.AsNullable(), "quality_drink", "quality_verygood"); break;
            case 5: _mood.AddMood(ent.AsNullable(), "quality_drink", "quality_fantastic"); break;
            case 6: _mood.AddMood(ent.AsNullable(), "quality_food", "amazingtaste"); break;
        }
    }

    #endregion

    #region Бафф

    /// <summary>apply_buff: food/haste силой со сложность блюда.</summary>
    private void ApplyBuff(EntityUid eater, int complexity)
    {
        if (complexity <= 0)
            return;

        var buff = EnsureComp<ImperialFoodBuffComponent>(eater);
        buff.Strength = Math.Min(complexity, 5);
        buff.End = _timing.CurTime + BuffDuration * TimeoutMod;
        _alerts.ShowAlert(eater, BuffAlert, (short) buff.Strength);
        _speed.RefreshMovementSpeedModifiers(eater);
    }

    /// <summary>movespeed food_haste: −0.04 × сила к задержке шага TG.</summary>
    private void OnBuffSpeed(Entity<ImperialFoodBuffComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var modifier = ImperialMood.RunDelay / (ImperialMood.RunDelay - 0.04f * ent.Comp.Strength);
        args.ModifySpeed(modifier, modifier);
    }

    #endregion
}
