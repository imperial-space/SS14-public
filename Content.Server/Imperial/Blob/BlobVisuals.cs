using Content.Shared.Imperial.Blob;
using Robust.Shared.Maths;

namespace Content.Server.Imperial.Blob;

public static class BlobVisuals
{
    public static Color GetChemicalColor(BlobChemicalType chemical)
    {
        return chemical switch
        {
            BlobChemicalType.Toxin => Color.FromHex("#7fbf3f"),
            BlobChemicalType.Incendiary => Color.FromHex("#ff7a2f"),
            BlobChemicalType.Electromagnetic => Color.FromHex("#5bc0ff"),
            BlobChemicalType.DistributedNeurons => Color.FromHex("#f2da5a"),
            BlobChemicalType.Regenerative => Color.FromHex("#58d68d"),
            BlobChemicalType.KineticGelatin => Color.FromHex("#d8a0ff"),
            BlobChemicalType.RadioactiveGel => Color.FromHex("#9adf4f"),
            BlobChemicalType.LexorinJelly => Color.FromHex("#bfbfbf"),
            BlobChemicalType.CryogenicLiquid => Color.FromHex("#7fe7ff"),
            BlobChemicalType.Sorium => Color.FromHex("#ffd166"),
            BlobChemicalType.EnvenomedFilaments => Color.FromHex("#6bcf6b"),
            BlobChemicalType.ParalyticToxins => Color.FromHex("#b18cff"),
            _ => Color.White,
        };
    }
}