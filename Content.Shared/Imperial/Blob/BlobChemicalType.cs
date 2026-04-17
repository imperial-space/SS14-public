using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public enum BlobChemicalType : byte
{
    Toxin,
    Incendiary,
    Electromagnetic,
    DistributedNeurons,
    Regenerative,
    KineticGelatin,
    RadioactiveGel,
    LexorinJelly,
    CryogenicLiquid,
    Sorium,
    EnvenomedFilaments,
    ParalyticToxins,
}