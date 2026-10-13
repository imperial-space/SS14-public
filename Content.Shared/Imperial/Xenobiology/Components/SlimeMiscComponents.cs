using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>status_effect/slimed: жертву покрывает разъедающая слизь.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SlimedComponent : Component
{
    /// <summary>slime_stacks: смываются водой.</summary>
    [DataField, AutoNetworkedField]
    public float Stacks = 10f;

    /// <summary>slime_color.</summary>
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTick;
}

/// <summary>obj/item/storage/bag/xeno: научная сумка, из неё слайму скармливают экстракты для кроссбридинга.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeExtractBagComponent : Component;

/// <summary>obj/item/slimecross: скрещенный экстракт.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlimeCrossbreedComponent : Component
{
    /// <summary>colour.</summary>
    [DataField(required: true)]
    public ProtoId<SlimeTypePrototype> SlimeType;

    /// <summary>effect: модификация (burning, charged...).</summary>
    [DataField(required: true)]
    public string Effect = string.Empty;

    /// <summary>effect_desc.</summary>
    [DataField]
    public LocId? EffectDesc;
}
