using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

[Serializable, NetSerializable]
public sealed class QuantumConsoleDomainEntry(string id, string name, string description, int cost, BitrunningDifficulty difficulty, string reward, bool modular, bool secondary, bool ghosts = false)
{
    public bool Ghosts = ghosts;
    public string Id = id;
    public string Name = name;
    public string Description = description;
    public int Cost = cost;
    public BitrunningDifficulty Difficulty = difficulty;
    public string Reward = reward;
    public bool Modular = modular;
    public bool Secondary = secondary;
}
