using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

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
