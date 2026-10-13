using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>
/// datum/slime_type: цвет слайма, его экстракт и возможные мутации.
/// </summary>
[Prototype]
public sealed partial class SlimeTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>colour: префикс состояний спрайта (grey, dark-purple...).</summary>
    [DataField(required: true)]
    public string Colour = string.Empty;

    /// <summary>Название цвета для имени слайма.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>transparent: полупрозрачные спрайты (alpha 180).</summary>
    [DataField]
    public bool Transparent;

    /// <summary>core_type: экстракт этого цвета.</summary>
    [DataField(required: true)]
    public EntProtoId Core;

    /// <summary>mutations: возможные мутации потомков и их вес.</summary>
    [DataField]
    public Dictionary<ProtoId<SlimeTypePrototype>, float> Mutations = new();

    /// <summary>rgb_code: цвет, которым слайм пачкает жертву.</summary>
    [DataField]
    public Color RgbCode = Color.White;

    /// <summary>Уровень цвета (0–5, радужный — особый).</summary>
    [DataField]
    public int Tier;
}
