using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>tool_interact(): резка открытой капсулы сварочным аппаратом.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialSupplyPodCutDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
