using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Панель ядра (screwdriver_act у core).</summary>
[RegisterComponent]
public sealed partial class HypertorusCorePanelComponent : Component
{
    [ViewVariables] public bool PanelOpen;

    [DataField]
    public EntProtoId Box = "ImperialHypertorusBoxCore";
}
