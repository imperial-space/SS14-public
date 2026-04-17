using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Blob;

[Serializable, NetSerializable]
public enum BlobChemicalMenuBuiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class BlobChemicalMenuState : BoundUserInterfaceState
{
    public BlobChemicalType CurrentChemical { get; }
    public List<BlobChemicalType> Chemicals { get; }
    public int Biomass { get; }
    public int ChangeCost { get; }
    public Color CurrentColor { get; }

    public BlobChemicalMenuState(BlobChemicalType currentChemical, List<BlobChemicalType> chemicals, int biomass, int changeCost, Color currentColor)
    {
        CurrentChemical = currentChemical;
        Chemicals = chemicals;
        Biomass = biomass;
        ChangeCost = changeCost;
        CurrentColor = currentColor;
    }
}

[Serializable, NetSerializable]
public sealed class BlobSelectChemicalMessage : BoundUserInterfaceMessage
{
    public BlobChemicalType Chemical { get; }

    public BlobSelectChemicalMessage(BlobChemicalType chemical)
    {
        Chemical = chemical;
    }
}