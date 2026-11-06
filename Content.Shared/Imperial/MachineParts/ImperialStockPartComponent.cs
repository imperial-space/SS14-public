using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.MachineParts;

/// <summary>
/// Стандартная деталь машины с уровнем (rating). Засчитывается вместо универсальной детали
/// («Manipulator») в рамке машины, а её уровень влияет на работу машины.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialStockPartComponent : Component
{
    [DataField("partType", required: true), AutoNetworkedField]
    public ImperialStockPartType Type;

    [DataField, AutoNetworkedField]
    public int Tier = 1;
}
