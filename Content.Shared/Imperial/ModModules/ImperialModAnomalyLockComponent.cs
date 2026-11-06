using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// anomaly_locked: модуль работает только с ядром аномалии нужного типа внутри.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialModAnomalyLockComponent : Component
{
    public const string ContainerId = "imperial-mod-core";

    [DataField(required: true)]
    public List<EntProtoId> Cores = new();

    /// <summary>core_removable.</summary>
    [DataField]
    public bool Removable = true;
}
