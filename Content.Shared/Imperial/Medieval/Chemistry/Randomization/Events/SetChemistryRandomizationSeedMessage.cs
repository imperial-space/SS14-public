using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.ChemistryRandomization;

[Serializable, NetSerializable]
public sealed partial class SetChemistryRandomizationSeedMessage : EntityEventArgs
{
    public readonly int Seed;

    public SetChemistryRandomizationSeedMessage(int seed)
    {
        Seed = seed;
    }
}
