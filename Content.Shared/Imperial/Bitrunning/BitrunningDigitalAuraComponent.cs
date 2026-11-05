using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>create_digital_aura(): красная «цифровая» подсветка и полупрозрачность глюка.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BitrunningDigitalAuraComponent : Component;
