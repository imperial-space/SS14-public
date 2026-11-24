namespace Content.Shared.Lathe;

public sealed partial class LatheGetRecipesEvent
{
    public readonly List<WeeklyLatheRecipeData> WeeklyRecipes = new();
}
