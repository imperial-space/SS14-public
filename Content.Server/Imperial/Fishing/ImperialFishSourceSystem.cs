using System.Linq;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Источники рыбы (/datum/fish_source): таблица улова, сложность, награды и ограниченные количества.
/// </summary>
public sealed partial class ImperialFishSourceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ImperialFishingBaitSystem _bait = default!;
    [Dependency] private readonly ImperialFishDataSystem _data = default!;
    [Dependency] private readonly ImperialFishTraitSystem _traits = default!;
    [Dependency] private readonly ImperialFishingSkillSystem _skill = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>Множители веса рыбы по качеству наживки (weight_result_multiplier).</summary>
    private static readonly Dictionary<ImperialBaitQuality, float> ResultMultiplier = new()
    {
        [ImperialBaitQuality.Great] = 9,
        [ImperialBaitQuality.Good] = 3.5f,
        [ImperialBaitQuality.Basic] = 2,
    };

    /// <summary>Выравнивание весов (weight_leveling_exponents).</summary>
    private static readonly Dictionary<ImperialBaitQuality, float> LevelingExponent = new()
    {
        [ImperialBaitQuality.Great] = 0.7f,
        [ImperialBaitQuality.Good] = 0.55f,
        [ImperialBaitQuality.Basic] = 0.4f,
    };

    /// <summary>Текущие ограниченные количества по источникам.</summary>
    private readonly Dictionary<string, Dictionary<string, int>> _counts = new();

    /// <summary>Сколько единиц каждого улова ждёт восстановления и когда.</summary>
    private readonly List<(string Source, string Reward, TimeSpan At)> _regen = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateChasmStorage();

        for (var i = _regen.Count - 1; i >= 0; i--)
        {
            var (source, reward, at) = _regen[i];
            if (_timing.CurTime < at)
                continue;

            _regen.RemoveAt(i);
            var counts = GetCounts(source);
            counts[reward] = counts.GetValueOrDefault(reward) + 1;
        }
    }

    private Dictionary<string, int> GetCounts(string sourceId)
    {
        if (_counts.TryGetValue(sourceId, out var counts))
            return counts;

        counts = _proto.TryIndex<ImperialFishSourcePrototype>(sourceId, out var source)
            ? new Dictionary<string, int>(source.Counts)
            : new Dictionary<string, int>();
        _counts[sourceId] = counts;
        return counts;
    }

    /// <summary>get_fish_table(): таблица без исчерпанных наград.</summary>
    public Dictionary<string, float> GetTable(ImperialFishSourcePrototype source)
    {
        var counts = GetCounts(source.ID);
        var table = new Dictionary<string, float>();
        var baseTable = source.Special == "portalRandom" ? GetRandomPortalTable() : source.Table.ToDictionary(p => p.Key, p => (float) p.Value);
        foreach (var (reward, weight) in baseTable)
        {
            if (counts.TryGetValue(reward, out var left) && left <= 0)
                continue;

            table[reward] = weight;
        }

        return table;
    }

    /// <summary>get_modified_fish_table(): таблица с учётом наживки, крючка и черт.</summary>
    public Dictionary<string, float> GetModifiedTable(ImperialFishSourcePrototype source, ImperialFishingRodInfo rod, EntityUid user, EntityUid location)
    {
        var table = GetTable(source);
        var leveling = 0f;
        var resultMult = 1f;
        var quality = rod.Bait is { } baitEnt ? _bait.GetQuality(baitEnt) : ImperialBaitQuality.None;
        if (rod.Bait != null && ResultMultiplier.TryGetValue(quality, out var qualityMult))
        {
            resultMult = qualityMult;
            leveling = LevelingExponent[quality];
        }

        if (RemovesDud(rod))
            table.Remove(ImperialFishing.Dud);

        foreach (var reward in table.Keys.ToList())
        {
            var weight = table[reward];
            var isFish = _data.IsFish(reward);
            weight *= HookMultiplier(rod.HookComp, reward, isFish);

            if (isFish)
            {
                if (rod.Bait is { } bait)
                {
                    weight = MathF.Round(weight * resultMult);
                    var mult = _bait.CheckBait(bait, reward);
                    weight = MathF.Round(weight * mult);
                    if (mult > 1 && _bait.AllowsDud(bait))
                        table.Remove(ImperialFishing.Dud);
                }
                else
                {
                    weight = MathF.Round(weight * ImperialFishing.WeightMultWithoutBait);
                }

                weight = GetTraitCatchMods(weight, reward, rod, user, location);
            }
            else if (rod.Bait != null && (quality is ImperialBaitQuality.Good or ImperialBaitQuality.Great) && IsMobReward(reward))
            {
                weight = MathF.Round(weight * resultMult);
            }

            if (weight <= 0)
                table.Remove(reward);
            else if (table.ContainsKey(reward))
                table[reward] = weight;
        }

        if (leveling > 0)
            LevelOutFish(table, leveling);

        return table;
    }

    private bool IsMobReward(string reward)
    {
        return _proto.TryIndex<EntityPrototype>(reward, out var proto) && proto.Components.ContainsKey("MobState");
    }

    private bool RemovesDud(ImperialFishingRodInfo rod)
    {
        if (rod.HookComp?.RemoveDud == true)
            return true;

        // Любая наживка убирает «пустой» улов, кроме денег и блёсен.
        return rod.Bait is { } bait && !_bait.AllowsDud(bait);
    }

    /// <summary>get_hook_bonus_multiplicative().</summary>
    private static float HookMultiplier(ImperialFishingHookComponent? hook, string reward, bool isFish)
    {
        if (hook == null || reward == ImperialFishing.Dud)
            return 1;

        return isFish ? hook.FishMultiplier : hook.NonFishMultiplier;
    }

    /// <summary>level_out_fish(): редкие рыбы становятся чаще.</summary>
    private void LevelOutFish(Dictionary<string, float> table, float exponent)
    {
        var fishWeights = table.Where(p => _data.IsFish(p.Key)).ToList();
        if (fishWeights.Count == 0)
            return;

        var highest = fishWeights.Max(p => p.Value);
        foreach (var (fish, weight) in fishWeights)
        {
            var difference = highest - weight;
            if (difference > 0)
                table[fish] += MathF.Round(MathF.Pow(difference, exponent));
        }
    }

    private IEnumerable<string> GetFishTraits(string fishProto, ImperialFishComponent? instance)
    {
        if (instance != null)
            return instance.Traits.Select(t => t.Id);

        var data = _data.GetFish(fishProto);
        return data?.InitialTraits.Select(t => t.Id) ?? Enumerable.Empty<string>();
    }

    private float GetTraitCatchMods(float weight, string fishProto, ImperialFishingRodInfo rod, EntityUid user, EntityUid location)
    {
        var multiplier = 1f;
        foreach (var trait in GetFishTraits(fishProto, null))
        {
            var (add, mult) = _traits.CatchWeightMod(trait, rod, user, location, fishProto);
            weight += add;
            multiplier *= mult;
        }

        return MathF.Round(weight * multiplier);
    }

    /// <summary>roll_reward().</summary>
    public string RollReward(ImperialFishSourcePrototype source, ImperialFishingRodInfo rod, EntityUid user, EntityUid location)
    {
        var table = GetModifiedTable(source, rod, user, location);
        return PickWeighted(table) ?? ImperialFishing.Dud;
    }

    public string? PickWeighted(Dictionary<string, float> table)
    {
        var total = table.Values.Where(v => v > 0).Sum();
        if (total <= 0)
            return null;

        var roll = _random.NextFloat() * total;
        foreach (var (key, weight) in table)
        {
            if (weight <= 0)
                continue;

            roll -= weight;
            if (roll <= 0)
                return key;
        }

        return table.Keys.Last();
    }

    /// <summary>calculate_difficulty(): сложность источника, удочки, рыбы, наживки и черт.</summary>
    public float CalculateDifficulty(ImperialFishSourcePrototype source, string? reward, ImperialFishComponent? rewardInstance, ImperialFishingRodInfo rod, EntityUid user)
    {
        var result = CalculateDifficultyInner(source, reward, rewardInstance, rod, user);

        // /datum/fish_source/portal/random/calculate_difficulty().
        if (source.Special == "portalRandom")
            result += _random.Next(-10, 16);

        return result;
    }

    private float CalculateDifficultyInner(ImperialFishSourcePrototype source, string? reward, ImperialFishComponent? rewardInstance, ImperialFishingRodInfo rod, EntityUid user)
    {
        var difficulty = source.Difficulty + rod.Comp.DifficultyModifier;

        var fish = rewardInstance ?? (reward != null ? _data.GetFish(reward) : null);
        if (fish == null)
            return source.Special == "vending" ? difficulty + GetVendingDifficulty(reward, rod) : difficulty;

        if (source.Special == "vending")
            return difficulty + GetVendingDifficulty(reward, rod);

        difficulty += fish.DifficultyModifier;

        if (rod.Bait is { } bait)
        {
            foreach (var id in fish.FavoriteBait)
            {
                if (_bait.Matches(bait, id))
                    difficulty += ImperialFishing.FavBaitDifficultyMod;
            }

            foreach (var id in fish.DislikedBait)
            {
                if (_bait.Matches(bait, id))
                    difficulty += ImperialFishing.DislikedBaitDifficultyMod;
            }
        }

        var additive = 0f;
        var multiplicative = 1f;
        foreach (var trait in rewardInstance != null ? rewardInstance.Traits.Select(t => t.Id) : GetFishTraits(reward!, null))
        {
            var (add, mult) = _traits.DifficultyMod(trait, rod, user);
            additive += add;
            multiplicative *= mult;
        }

        difficulty += additive;
        difficulty *= multiplicative;
        return difficulty;
    }

    /// <summary>
    /// simple_dispense_reward() + spawn_reward(): создать награду у ног рыбака, учтя ограниченные количества.
    /// </summary>
    public EntityUid? SpawnReward(ImperialFishSourcePrototype source, string reward, Robust.Shared.Map.EntityCoordinates coords)
    {
        if (reward == ImperialFishing.Dud)
            return null;

        var counts = GetCounts(source.ID);
        if (counts.TryGetValue(reward, out var left))
        {
            if (left <= 0)
                return null;

            counts[reward] = left - 1;
            var regen = source.CountRegen.TryGetValue(reward, out var time) ? time : TimeSpan.FromMinutes(30);
            _regen.Add((source.ID, reward, _timing.CurTime + regen));
        }

        if (TrySpecialReward(source, reward, coords, out var special))
            return special;

        if (!_proto.HasIndex<EntityPrototype>(reward))
        {
            Log.Error($"Неизвестная награда рыбалки {reward} в источнике {source.ID}");
            return null;
        }

        var entity = Spawn(reward, coords);
        if (TryComp<ImperialFishComponent>(entity, out var fish))
        {
            fish.JustSpawned = true;
            if (source.Special == "portalRandom")
                RandomizePortalFish((entity, fish));
            else
                _fish.RandomizeSizeAndWeight((entity, fish));
        }

        return entity;
    }

    /// <summary>dispense_reward(): награду в руки, сообщение и т. п.</summary>
    public EntityUid? DispenseReward(ImperialFishSourcePrototype source, string reward, EntityUid fisherman)
    {
        var spawned = SpawnReward(source, reward, Transform(fisherman).Coordinates);
        if (spawned == null)
        {
            _popup.PopupEntity(Loc.GetString(_random.Pick(source.Duds)), fisherman, fisherman);
            return null;
        }

        _hands.PickupOrDrop(fisherman, spawned.Value);
        _popup.PopupEntity(Loc.GetString("imperial-fishing-caught", ("reward", spawned.Value)), fisherman, fisherman);
        return spawned;
    }

    /// <summary>readd_fish(): выпущенная живая рыба возвращается в ограниченное количество.</summary>
    public void ReaddFish(ImperialFishSourcePrototype source, EntityUid fish)
    {
        if (!TryComp<ImperialFishComponent>(fish, out var comp) || comp.Dead || MetaData(fish).EntityPrototype is not { } proto)
            return;

        if (!source.Table.ContainsKey(proto.ID) || !source.Counts.ContainsKey(proto.ID))
            return;

        var pending = _regen.FindIndex(r => r.Source == source.ID && r.Reward == proto.ID);
        if (pending >= 0)
            _regen.RemoveAt(pending);

        var counts = GetCounts(source.ID);
        counts[proto.ID] = counts.GetValueOrDefault(proto.ID) + 1;
    }

    /// <summary>Знает ли рыбак эту рыбу (has_known_fishes / get_catchable_fish_names).</summary>
    public List<string> GetCatchableFishNames(ImperialFishSourcePrototype source)
    {
        var names = new List<string>();
        foreach (var reward in GetTable(source).Keys)
        {
            var fish = _data.GetFish(reward);
            if (fish == null || !fish.ShowInCatalog || !_proto.TryIndex<EntityPrototype>(reward, out var proto))
                continue;

            names.Add(proto.Name);
        }

        return names;
    }
}
