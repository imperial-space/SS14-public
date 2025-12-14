using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.ChemistryRandomization;

[Serializable, NetSerializable]
public sealed partial class RequestChemistryRandomizationSeedMessage : EntityEventArgs
{
    public readonly int Seed;
    public readonly string Username;

    public RequestChemistryRandomizationSeedMessage(int seed, string name)
    {
        Seed = seed;
        Username = name;
    }
}
