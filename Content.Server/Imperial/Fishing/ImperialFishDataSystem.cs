using Content.Shared.Imperial.Fishing;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Свойства видов рыб по прототипам (SSfishing.fish_properties) и спонтанные черты (GLOB.spontaneous_fish_traits).
/// </summary>
public sealed class ImperialFishDataSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    private readonly Dictionary<string, ImperialFishComponent?> _fish = new();
    private Dictionary<string, Dictionary<string, int>>? _spontaneous;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnReload);
    }

    private void OnReload(PrototypesReloadedEventArgs args)
    {
        _fish.Clear();
        _spontaneous = null;
    }

    /// <summary>Данные вида рыбы из прототипа (или null, если это не рыба).</summary>
    public ImperialFishComponent? GetFish(string protoId)
    {
        if (_fish.TryGetValue(protoId, out var cached))
            return cached;

        ImperialFishComponent? fish = null;
        if (_proto.TryIndex<EntityPrototype>(protoId, out var proto))
            proto.TryGetComponent(out fish, _factory);

        _fish[protoId] = fish;
        return fish;
    }

    public bool IsFish(string protoId) => GetFish(protoId) != null;

    /// <summary>Черты, которые вид может получить спонтанно, с шансом.</summary>
    public Dictionary<string, int> GetSpontaneousTraitChances(string fishProto)
    {
        _spontaneous ??= BuildSpontaneous();
        return _spontaneous.TryGetValue(fishProto, out var list) ? list : new Dictionary<string, int>();
    }

    public IEnumerable<string> GetSpontaneousTraits(string fishProto)
    {
        foreach (var (trait, chance) in GetSpontaneousTraitChances(fishProto))
        {
            if (chance > 0)
                yield return trait;
        }
    }

    private Dictionary<string, Dictionary<string, int>> BuildSpontaneous()
    {
        var result = new Dictionary<string, Dictionary<string, int>>();
        foreach (var trait in _proto.EnumeratePrototypes<ImperialFishTraitPrototype>())
        {
            foreach (var (fishProto, chance) in trait.Spontaneous)
            {
                // zebra_typecacheof: наследники вида получают ту же вероятность, если не указано иное.
                foreach (var entity in _proto.EnumeratePrototypes<EntityPrototype>())
                {
                    if (entity.ID != fishProto && !InheritsFrom(entity.ID, fishProto))
                        continue;

                    if (entity.ID != fishProto && trait.Spontaneous.ContainsKey(entity.ID))
                        continue;

                    if (!result.TryGetValue(entity.ID, out var list))
                        result[entity.ID] = list = new Dictionary<string, int>();

                    list[trait.ID] = chance;
                }
            }
        }

        return result;
    }

    private bool InheritsFrom(string child, string parent)
    {
        foreach (var p in _proto.EnumerateParents<EntityPrototype>(child))
        {
            if (p.ID == parent)
                return true;
        }

        return false;
    }
}
