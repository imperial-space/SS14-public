using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/datum/crafting_recipe/food: блюдо собирается из того, что лежит рядом и в руках.</summary>
[Prototype]
public sealed partial class ImperialCraftRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>result_amount.</summary>
    [DataField]
    public int Amount = 1;

    /// <summary>time в секундах.</summary>
    [DataField]
    public float Time = 3f;

    /// <summary>reqs-предметы: прототип (или наследник) и количество.</summary>
    [DataField]
    public Dictionary<EntProtoId, int> Ingredients = new();

    /// <summary>reqs-реагенты из открытых ёмкостей рядом.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    /// <summary>dish_category TG.</summary>
    [DataField]
    public string Category = "Uncategorized";
}

/// <summary>Открыть меню кулинарии.</summary>
public sealed partial class ImperialCraftMenuActionEvent : InstantActionEvent;

/// <summary>Клиент → сервер: какие рецепты сейчас можно собрать.</summary>
[Serializable, NetSerializable]
public sealed class ImperialCraftStateRequest : EntityEventArgs;

/// <summary>Сервер → клиент: доступные для сборки рецепты.</summary>
[Serializable, NetSerializable]
public sealed class ImperialCraftStateMessage(HashSet<string> craftable) : EntityEventArgs
{
    public readonly HashSet<string> Craftable = craftable;
}

/// <summary>Клиент → сервер: собрать блюдо.</summary>
[Serializable, NetSerializable]
public sealed class ImperialCraftMessage(string recipe) : EntityEventArgs
{
    public readonly string Recipe = recipe;
}

[Serializable, NetSerializable]
public sealed partial class ImperialCraftDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Recipe = string.Empty;

    private ImperialCraftDoAfterEvent()
    {
    }

    public ImperialCraftDoAfterEvent(string recipe)
    {
        Recipe = recipe;
    }

    public override DoAfterEvent Clone() => this;
}
