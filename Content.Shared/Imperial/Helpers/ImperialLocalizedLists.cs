using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Helpers;

/// <summary>
/// Списки фраз и имён из <see cref="LocalizedDatasetPrototype"/> в виде готовых строк: текст живёт в ftl, а код
/// работает с обычным массивом. Результат кэшируется по ID датасета.
/// </summary>
public static class ImperialLocalizedLists
{
    private static readonly Dictionary<string, string[]> Cache = new();

    public static string[] Get(ProtoId<LocalizedDatasetPrototype> id)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(id, out var cached))
                return cached;
        }

        var proto = IoCManager.Resolve<IPrototypeManager>().Index(id);
        var result = new string[proto.Values.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Loc.GetString(proto.Values[i]);
        }

        lock (Cache)
        {
            Cache[id] = result;
        }

        return result;
    }
}
