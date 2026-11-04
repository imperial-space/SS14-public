using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Анализатор рыб (/obj/item/fish_analyzer): здоровье, голод, черты и эволюции рыбы или всех рыб в аквариуме.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishAnalyzerComponent : Component
{
    /// <summary>case_color: случайный цвет корпуса.</summary>
    [DataField, AutoNetworkedField]
    public Color CaseColor = Color.White;

    [ViewVariables]
    public EntityUid? Scanned;
}
