using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>Сетевой маркер аватара: клиенту нужен для статического шума при входе.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BitrunningAvatarMarkerComponent : Component;
