using Robust.Shared.Maths;

namespace Content.Shared.Imperial.Blob;

public static class BlobChemicalVisuals
{
    public static readonly BlobChemicalType[] SelectableChemicals =
    {
        BlobChemicalType.Sorium,
        BlobChemicalType.Toxin,
        BlobChemicalType.Incendiary,
        BlobChemicalType.Electromagnetic,
        BlobChemicalType.KineticGelatin,
        BlobChemicalType.RadioactiveGel,
        BlobChemicalType.LexorinJelly,
        BlobChemicalType.CryogenicLiquid,
        BlobChemicalType.EnvenomedFilaments,
    };

    public static string GetNameLocId(BlobChemicalType chemical)
    {
        return chemical switch
        {
            BlobChemicalType.Incendiary => "blob-chemical-incendiary",
            BlobChemicalType.Electromagnetic => "blob-chemical-electromagnetic",
            BlobChemicalType.DistributedNeurons => "blob-chemical-distributed-neurons",
            BlobChemicalType.Regenerative => "blob-chemical-regenerative",
            BlobChemicalType.KineticGelatin => "blob-chemical-kinetic-gelatin",
            BlobChemicalType.RadioactiveGel => "blob-chemical-radioactive-gel",
            BlobChemicalType.LexorinJelly => "blob-chemical-lexorin-jelly",
            BlobChemicalType.CryogenicLiquid => "blob-chemical-cryogenic-liquid",
            BlobChemicalType.Sorium => "blob-chemical-sorium",
            BlobChemicalType.EnvenomedFilaments => "blob-chemical-envenomed-filaments",
            BlobChemicalType.ParalyticToxins => "blob-chemical-paralytic-toxins",
            _ => "blob-chemical-toxin",
        };
    }

    public static Color GetColor(BlobChemicalType chemical)
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