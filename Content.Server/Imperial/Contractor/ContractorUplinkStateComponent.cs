using Content.Shared.Imperial.Contractor;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Contractor;

/// <summary>Состояние программы контрактника на конкретном планшете.</summary>
[RegisterComponent]
public sealed partial class ContractorUplinkStateComponent : Component
{
    /// <summary>traitor_data: разум, вошедший в программу.</summary>
    [ViewVariables]
    public EntityUid? LoggedInMind;

    [ViewVariables]
    public bool FirstLoad = true;

    [ViewVariables]
    public bool InfoScreen = true;

    [ViewVariables]
    public string Error = string.Empty;
}
