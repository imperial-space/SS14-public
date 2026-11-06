using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Плазменный сосуд (organ/alien/plasmavessel).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoPlasmaComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Plasma = 100;

    [DataField, AutoNetworkedField]
    public float MaxPlasma = 250;

    /// <summary>plasma_rate: прирост в секунду на сорняках (при ранах — половина), вне сорняков — 0.1 от него.</summary>
    [DataField]
    public float PlasmaRate = 5;

    /// <summary>heal_rate: лечение в секунду на сорняках, отдельно brute, burn и oxy.</summary>
    [DataField]
    public float HealRate = 2.5f;

    /// <summary>plasma_display: иконка power_display, число рисует клиент.</summary>
    [DataField]
    public string Alert = "XenoPlasma";
}
