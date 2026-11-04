using System.Linq;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Стазисный кейс для рыбы (/obj/item/storage/fish_case): одна рыба или предмет для аквариума,
/// размер кейса подстраивается под содержимое, рыба внутри в стазисе.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialFishCaseComponent : Component
{
    /// <summary>adjust_size: размер кейса растёт до размера содержимого.</summary>
    [DataField]
    public bool AdjustSize = true;

    [DataField]
    public ProtoId<ItemSizePrototype> BaseSize = "Small";

    /// <summary>random_fish_type(): случайная рыба по random_case_rarity, при заданной среде — совместимая с ней.</summary>
    [DataField]
    public bool RandomFish;

    [DataField]
    public ImperialFishFluid? RandomFluid;

    /// <summary>Взвешенный список рыб для заполнения (импортный, чёрный рынок).</summary>
    [DataField]
    public Dictionary<EntProtoId, float> Table = new();

    /// <summary>Источник ловли, из таблицы которого берутся рыбы (зловещий кейс Синдиката).</summary>
    [DataField]
    public ProtoId<ImperialFishSourcePrototype>? SourceTable;

    /// <summary>Рыба с чёрного рынка уже мертва.</summary>
    [DataField]
    public bool Dead;

    [ViewVariables]
    public bool Filled;
}
