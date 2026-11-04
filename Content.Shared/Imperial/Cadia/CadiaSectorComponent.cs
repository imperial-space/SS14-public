using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Сектор фронта: знамя, которое берут, оставшись рядом без врагов. Хозяину даёт снабжение (варп у Хаоса) и,
/// начиная со штурма, точку высадки, пока за сектор не идёт бой.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CadiaSectorComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Letter = "?";

    [DataField]
    public float Radius = 10f;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(90);

    [DataField, AutoNetworkedField]
    public CadiaFaction? Owner;

    [ViewVariables, AutoNetworkedField]
    public CadiaFaction? Capturer;

    [ViewVariables, AutoNetworkedField]
    public float Progress;

    [ViewVariables, AutoNetworkedField]
    public bool Contested;
}
