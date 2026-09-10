using Content.Shared.Research.Prototypes;
using NetSerializer;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Lathe;

[Serializable, NetSerializable]
public sealed class LatheUpdateState : BoundUserInterfaceState
{
    public List<ProtoId<LatheRecipePrototype>> Recipes;

    // Imperial Weekly Mode Start
    public List<WeeklyLatheRecipeData> WeeklyRecipes;
    // Imperial Weekly Mode End

    public LatheRecipeBatch[] Queue;

    // Imperial Weekly Mode: Original code removed:
    // public ProtoId<LatheRecipePrototype>? CurrentlyProducing;
    // Imperial Weekly Mode Start
    public string? CurrentlyProducing;

    public bool CurrentlyProducingIsWeekly;
    // Imperial Weekly Mode End

    public bool UseCardId; // Imperial PrinterDoc

    // Imperial Weekly Mode: Original code removed:
    // public LatheUpdateState(
    //     List<ProtoId<LatheRecipePrototype>> recipes,
    //     LatheRecipeBatch[] queue,
    //     ProtoId<LatheRecipePrototype>? currentlyProducing = null,
    //     bool useCardId = false) // Imperial PrinterDoc
    // Imperial Weekly Mode Start
    public LatheUpdateState(
        List<ProtoId<LatheRecipePrototype>> recipes,
        List<WeeklyLatheRecipeData> weeklyRecipes,
        LatheRecipeBatch[] queue,
        string? currentlyProducing = null,
        bool currentlyProducingIsWeekly = false,
        bool useCardId = false) // Imperial PrinterDoc
    // Imperial Weekly Mode End
    {
        Recipes = recipes;
        WeeklyRecipes = weeklyRecipes;
        Queue = queue;
        CurrentlyProducing = currentlyProducing;
        // Imperial Weekly Mode
        CurrentlyProducingIsWeekly = currentlyProducingIsWeekly;
        UseCardId = useCardId;
    }
}

/// <summary>
///     Sent to the server to sync material storage and the recipe queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheSyncRequestMessage : BoundUserInterfaceMessage
{

}

/// <summary>
///     Sent to the server when a client queues a new recipe.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheQueueRecipeMessage : BoundUserInterfaceMessage
{
    public readonly string ID;
    public readonly int Quantity;
    public LatheQueueRecipeMessage(string id, int quantity)
    {
        ID = id;
        Quantity = quantity;
    }
}

/// <summary>
///     Sent to the server to remove a batch from the queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheDeleteRequestMessage(int index) : BoundUserInterfaceMessage
{
    public int Index = index;
}

/// <summary>
///     Sent to the server to move the position of a batch in the queue.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheMoveRequestMessage(int index, int change) : BoundUserInterfaceMessage
{
    public int Index = index;
    public int Change = change;
}

/// <summary>
///     Sent to the server to stop producing the current item.
/// </summary>
[Serializable, NetSerializable]
public sealed class LatheAbortFabricationMessage() : BoundUserInterfaceMessage
{
}

[NetSerializable, Serializable]
public enum LatheUiKey
{
    Key,
}
