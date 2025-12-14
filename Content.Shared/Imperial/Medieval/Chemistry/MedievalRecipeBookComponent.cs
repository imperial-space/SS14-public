using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Chemistry;

[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalRecipeBookComponent : Component
{
    public List<string> Recipes = new();
}
[NetSerializable, Serializable]
public enum RecipeBookUi : byte
{
    Key
}
