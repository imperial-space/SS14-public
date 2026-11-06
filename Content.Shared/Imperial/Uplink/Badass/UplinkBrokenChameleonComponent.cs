using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Сломанный хамелеон: при создании принимает случайный облик и больше не меняется.</summary>
[RegisterComponent]
public sealed partial class UplinkBrokenChameleonComponent : Component;
