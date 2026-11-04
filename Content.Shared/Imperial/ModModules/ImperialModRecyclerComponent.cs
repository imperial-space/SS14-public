using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>recycler: собирает мусор под ногами и перерабатывает в листы.</summary>
[RegisterComponent]
public sealed partial class ImperialModRecyclerComponent : Component
{
    [DataField]
    public List<ProtoId<Content.Shared.Tag.TagPrototype>> Tags = new() { "Trash" };

    /// <summary>Накопленные материалы (единицы материала).</summary>
    [ViewVariables]
    public Dictionary<string, int> Materials = new();

    [ViewVariables]
    public TimeSpan NextPickup;
}
