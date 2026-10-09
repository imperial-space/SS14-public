using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

/// <summary>
/// Штамм блоба (datum/blobstrain из SS13): цвет, описания и бонусы.
/// Поведение конкретного штамма определяется <see cref="Kind"/> в серверной системе.
/// </summary>
[Prototype]
public sealed partial class BlobStrainPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public BlobStrainKind Kind;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>effectdesc: особые свойства тайлов.</summary>
    [DataField]
    public LocId? EffectDesc;

    /// <summary>shortdesc: краткое описание для миньонов.</summary>
    [DataField]
    public LocId? ShortDesc;

    [DataField(required: true)]
    public LocId AnalyzerDamage;

    [DataField]
    public LocId? AnalyzerEffect;

    [DataField]
    public Color Color = Color.Black;

    /// <summary>complementary_color: цвет оверманда, лечения и вен блоббернаута.</summary>
    [DataField]
    public Color ComplementaryColor = Color.Black;

    [DataField]
    public LocId Message = "blob-strain-message-default";

    [DataField]
    public LocId? MessageLiving;

    [DataField]
    public LocId BlobbernautMessage = "blob-strain-blobbernaut-slams";

    /// <summary>Реагент штамма: облако спор и «материал» блоба.</summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public float CoreRegenBonus;

    [DataField]
    public float PointRateBonus;

    /// <summary>Может ли выпасть при случайном выборе (GLOB.valid_blobstrains).</summary>
    [DataField]
    public bool Selectable = true;
}

[Serializable, NetSerializable]
public enum BlobStrainKind : byte
{
    BlazingOil,
    CryogenicPoison,
    DebrisDevourer,
    DistributedNeurons,
    ElectromagneticWeb,
    EnergizedJelly,
    ExplosiveLattice,
    NetworkedFibers,
    PressurizedSlime,
    ReactiveSpines,
    RegenerativeMateria,
    ReplicatingFoam,
    ShiftingFragments,
    SynchronousMesh,
}
