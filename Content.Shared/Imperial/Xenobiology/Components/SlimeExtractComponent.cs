using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// obj/item/slime_extract: экстракт слайма. В него впрыскивают реагенты, и он реагирует (slime_extracts.dm).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeExtractComponent : Component
{
    /// <summary>Цвет слайма, из которого получен экстракт.</summary>
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<SlimeTypePrototype> SlimeType;

    /// <summary>extract_uses.</summary>
    [DataField, AutoNetworkedField]
    public int Uses = 1;

    /// <summary>crossbreed_modification.</summary>
    [DataField]
    public string? CrossbreedModification;

    /// <summary>recurring: перезаряжающийся экстракт (кроссбридинг), его нельзя усилить.</summary>
    [DataField, AutoNetworkedField]
    public bool Recurring;

    [DataField]
    public string SolutionName = "extract";

    /// <summary>qdel_timer: отложенное удаление после реакции с задержкой.</summary>
    [ViewVariables]
    public TimeSpan? DeleteAt;

    /// <summary>Последний, кто держал экстракт (fingerprintslast).</summary>
    [ViewVariables]
    public EntityUid? LastHolder;
}

[Serializable, Robust.Shared.Serialization.NetSerializable]
public enum SlimePotionKind : byte
{
    /// <summary>slimepotion/slime/docility.</summary>
    Docility,
    /// <summary>slimepotion/sentience.</summary>
    Sentience,
    /// <summary>slimepotion/transference.</summary>
    Transference,
    /// <summary>slimepotion/slime/steroid.</summary>
    Steroid,
    /// <summary>slimepotion/enhancer.</summary>
    Enhancer,
    /// <summary>slimepotion/enhancer/max.</summary>
    MaxEnhancer,
    /// <summary>slimepotion/slime/stabilizer.</summary>
    Stabilizer,
    /// <summary>slimepotion/slime/mutator.</summary>
    Mutator,
    /// <summary>slimepotion/speed.</summary>
    Speed,
    /// <summary>slimepotion/fireproof.</summary>
    Fireproof,
    /// <summary>slimepotion/genderchange.</summary>
    GenderChange,
    /// <summary>slimepotion/renaming.</summary>
    Renaming,
    /// <summary>slimepotion/slimeradio.</summary>
    Radio,
}

/// <summary>obj/item/slimepotion.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimePotionComponent : Component
{
    [DataField(required: true)]
    public SlimePotionKind Kind;

    /// <summary>uses (у зелья огнеупорности их три).</summary>
    [DataField, AutoNetworkedField]
    public int Uses = 1;

    /// <summary>Зелье применяется только к слаймам (slimepotion/slime).</summary>
    [DataField]
    public bool SlimeOnly;

    /// <summary>being_used / prompted.</summary>
    [ViewVariables]
    public bool InUse;
}

/// <summary>Предмет или одежда уже обработаны зельем (TRAIT_SPEED_POTIONED, огнеупорность).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimePotionedComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Speed;

    [DataField, AutoNetworkedField]
    public bool Fireproof;

    /// <summary>slimepotion/lavaproof.</summary>
    [DataField, AutoNetworkedField]
    public bool Lavaproof;

    /// <summary>slimepotion/spaceproof.</summary>
    [DataField, AutoNetworkedField]
    public bool Spaceproof;
}
