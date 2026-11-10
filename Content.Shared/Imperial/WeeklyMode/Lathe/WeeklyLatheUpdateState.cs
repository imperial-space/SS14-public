using Content.Shared.Research.Prototypes;
using NetSerializer;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Lathe;

[Serializable, NetSerializable]
public sealed class WeeklyLatheUpdateState : BoundUserInterfaceState
{
    public List<ProtoId<LatheRecipePrototype>> Recipes;

    public List<WeeklyLatheRecipeData> WeeklyRecipes;

    public LatheRecipeBatch[] Queue;

    public string? CurrentlyProducing;

    public bool CurrentlyProducingIsWeekly;

    public bool UseCardId; // Imperial PrinterDoc

    public WeeklyLatheUpdateState(
        List<ProtoId<LatheRecipePrototype>> recipes,
        List<WeeklyLatheRecipeData> weeklyRecipes,
        LatheRecipeBatch[] queue,
        string? currentlyProducing = null,
        bool currentlyProducingIsWeekly = false,
        bool useCardId = false) // Imperial PrinterDoc
    {
        Recipes = recipes;
        WeeklyRecipes = weeklyRecipes;
        Queue = queue;
        CurrentlyProducing = currentlyProducing;
        CurrentlyProducingIsWeekly = currentlyProducingIsWeekly;
        UseCardId = useCardId;
    }
}
