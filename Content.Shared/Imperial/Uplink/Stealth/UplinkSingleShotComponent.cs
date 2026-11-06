using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Одноразовый «пистолет» воздушного поцелуя: исчезает после выстрела.</summary>
[RegisterComponent]
public sealed partial class UplinkSingleShotComponent : Component;
