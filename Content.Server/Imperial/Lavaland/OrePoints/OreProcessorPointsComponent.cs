using Content.Shared.Materials;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.Imperial.Lavaland.OrePoints;

[RegisterComponent]
public sealed partial class OreProcessorPointsComponent : Component
{
    [DataField]
    public int StoredPoints;

    [DataField]
    public int MaterialUnitVolume = 100;

    [DataField("materialPointValues", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, MaterialPrototype>))]
    public Dictionary<string, int> MaterialPointValues = new()
    {
        { "RawIron", 1 },
        { "RawSilver", 16 },
        { "RawPlasma", 15 },
        { "RawGold", 18 },
        { "RawUranium", 30 },
        { "RawDiamond", 50 },
        { "RawBananium", 60 },
        { "RawQuartz", 1 },
        { "Coal", 1 },
    };
}
