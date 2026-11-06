using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>
/// Генератор атмосферного щита (atmos_shield_gen): два генератора лицом друг к другу
/// на расстоянии до maxRange тайлов держат между собой газонепроницаемое поле.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialAtmosShieldGenComponent : Component
{
    [DataField]
    public int MaxRange = 2;

    [DataField]
    public EntProtoId Field = "ImperialAtmosShieldField";

    [ViewVariables]
    public List<EntityUid> Fields = new();

    [ViewVariables]
    public TimeSpan NextCheck;

    [DataField]
    public TimeSpan CheckDelay = TimeSpan.FromSeconds(1);
}
