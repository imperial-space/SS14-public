using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>obj/item/storage/bag/xeno: научная сумка, из неё слайму скармливают экстракты для кроссбридинга.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeExtractBagComponent : Component;
