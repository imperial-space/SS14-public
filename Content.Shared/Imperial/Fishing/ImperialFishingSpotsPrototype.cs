using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Какие существующие сущности сборки считаются местами ловли (вода, лава, пропасть, унитаз и т. п.),
/// без изменения их прототипов.
/// </summary>
[Prototype]
public sealed partial class ImperialFishingSpotsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Прототип сущности (с наследниками) → источник рыбы.</summary>
    [DataField]
    public Dictionary<EntProtoId, ProtoId<ImperialFishSourcePrototype>> Entities = new();

    /// <summary>Компонент сущности → источник рыбы (например, Toilet).</summary>
    [DataField]
    public Dictionary<string, ProtoId<ImperialFishSourcePrototype>> Components = new();
}
